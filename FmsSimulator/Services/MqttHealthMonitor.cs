using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FmsSimulator.Models;

namespace FmsSimulator.Services
{
    /// <summary>
    /// MQTT Health Monitor - Monitors MQTT connection health and implements circuit breaker pattern.
    /// 
    /// Key Responsibilities:
    /// - Health checks with configurable intervals
    /// - Circuit breaker pattern (CLOSED → OPEN → HALF_OPEN)
    /// - Connection quality metrics (latency, packet loss, reconnection rate)
    /// - Automatic failure detection and recovery
    /// - Health status reporting
    /// </summary>
    public class MqttHealthMonitor : IDisposable
    {
        private readonly MqttClientService _mqttClient;
        private readonly MqttHighAvailabilitySettings _settings;
        private readonly ILogger<MqttHealthMonitor> _logger;
        private readonly LoggingService _structuredLogger = LoggingService.Instance;

        // Circuit breaker state
        private CircuitBreakerState _circuitState = CircuitBreakerState.Closed;
        private int _consecutiveFailures = 0;
        private DateTime _circuitOpenedAt = DateTime.MinValue;
        private readonly object _circuitLock = new();

        // Health check
        private CancellationTokenSource? _healthCheckCts;
        private Task? _healthCheckTask;
        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(30);

        // Metrics
        private readonly ConcurrentQueue<HealthCheckResult> _recentHealthChecks = new();
        private readonly int _maxHealthCheckHistory = 100;
        private long _totalHealthChecks = 0;
        private long _successfulHealthChecks = 0;
        private long _failedHealthChecks = 0;

        // Events
        public event EventHandler<CircuitBreakerStateChangedEventArgs>? CircuitBreakerStateChanged;
        public event EventHandler<HealthCheckFailedEventArgs>? HealthCheckFailed;

        public MqttHealthMonitor(
            MqttClientService mqttClient,
            IOptions<MqttSettings> mqttSettings,
            ILogger<MqttHealthMonitor> logger)
        {
            _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
            _settings = mqttSettings.Value?.MqttHighAvailability ?? throw new ArgumentNullException(nameof(mqttSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the health monitoring service.
        /// </summary>
        public void StartMonitoring()
        {
            if (_healthCheckTask != null && !_healthCheckTask.IsCompleted)
            {
                _logger.LogWarning("Health monitoring is already running.");
                return;
            }

            _healthCheckCts = new CancellationTokenSource();
            _healthCheckTask = Task.Run(() => HealthCheckLoopAsync(_healthCheckCts.Token));

            _logger.LogInformation("MQTT Health Monitor started. Interval: {Interval}s", _healthCheckInterval.TotalSeconds);
        }

        /// <summary>
        /// Stops the health monitoring service.
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (_healthCheckCts == null || _healthCheckTask == null)
                return;

            _healthCheckCts.Cancel();
            try
            {
                await _healthCheckTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            _logger.LogInformation("MQTT Health Monitor stopped.");
        }

        /// <summary>
        /// Main health check loop.
        /// </summary>
        private async Task HealthCheckLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_healthCheckInterval, cancellationToken);
                    await PerformHealthCheckAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health check loop.");
                }
            }
        }

        /// <summary>
        /// Performs a single health check.
        /// </summary>
        private async Task PerformHealthCheckAsync()
        {
            var result = new HealthCheckResult
            {
                Timestamp = DateTime.UtcNow
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Check 1: Connection status
                result.IsConnected = _mqttClient.IsConnected;

                if (!result.IsConnected)
                {
                    result.HealthStatus = HealthStatus.Unhealthy;
                    result.Message = "MQTT client is not connected to broker.";
                    await RecordFailureAsync(result);
                    return;
                }

                // Check 2: Ping test (publish to self-test topic)
                string testTopic = $"fms/health/ping/{Guid.NewGuid():N}";
                bool publishSuccess = await _mqttClient.PublishAsync(testTopic, "ping", retain: false);

                stopwatch.Stop();
                result.LatencyMs = stopwatch.ElapsedMilliseconds;

                if (!publishSuccess)
                {
                    result.HealthStatus = HealthStatus.Degraded;
                    result.Message = "Publish test failed (possible network issues).";
                    await RecordFailureAsync(result);
                    return;
                }

                // Check 3: Latency threshold
                if (result.LatencyMs > 1000) // 1 second threshold
                {
                    result.HealthStatus = HealthStatus.Degraded;
                    result.Message = $"High latency detected: {result.LatencyMs}ms";
                    _logger.LogWarning("MQTT connection degraded. Latency: {Latency}ms", result.LatencyMs);
                }
                else
                {
                    result.HealthStatus = HealthStatus.Healthy;
                    result.Message = $"All checks passed. Latency: {result.LatencyMs}ms";
                }

                await RecordSuccessAsync(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.HealthStatus = HealthStatus.Unhealthy;
                result.Message = $"Health check failed: {ex.Message}";
                result.Exception = ex;
                await RecordFailureAsync(result);
            }
        }

        /// <summary>
        /// Records a successful health check.
        /// </summary>
        private async Task RecordSuccessAsync(HealthCheckResult result)
        {
            Interlocked.Increment(ref _totalHealthChecks);
            Interlocked.Increment(ref _successfulHealthChecks);

            AddToHistory(result);

            // Reset consecutive failures
            lock (_circuitLock)
            {
                _consecutiveFailures = 0;

                // Transition from HALF_OPEN to CLOSED
                if (_circuitState == CircuitBreakerState.HalfOpen)
                {
                    ChangeCircuitState(CircuitBreakerState.Closed);
                }
            }

            _logger.LogDebug("Health check passed. Status: {Status}, Latency: {Latency}ms",
                result.HealthStatus, result.LatencyMs);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Records a failed health check.
        /// </summary>
        private async Task RecordFailureAsync(HealthCheckResult result)
        {
            Interlocked.Increment(ref _totalHealthChecks);
            Interlocked.Increment(ref _failedHealthChecks);

            AddToHistory(result);

            lock (_circuitLock)
            {
                _consecutiveFailures++;

                _logger.LogWarning(
                    "Health check failed. Consecutive failures: {Failures}/{Threshold}. Message: {Message}",
                    _consecutiveFailures, _settings.CircuitBreakerThreshold, result.Message);

                // Trigger circuit breaker if threshold exceeded
                if (_settings.EnableCircuitBreaker && 
                    _consecutiveFailures >= _settings.CircuitBreakerThreshold &&
                    _circuitState == CircuitBreakerState.Closed)
                {
                    ChangeCircuitState(CircuitBreakerState.Open);
                }
            }

            // Raise event
            HealthCheckFailed?.Invoke(this, new HealthCheckFailedEventArgs
            {
                Result = result,
                ConsecutiveFailures = _consecutiveFailures
            });

            _structuredLogger.LogOperationalMetrics("MqttHealthMonitor", "HealthCheckFailed", new System.Collections.Generic.Dictionary<string, object>
            {
                ["consecutiveFailures"] = _consecutiveFailures,
                ["healthStatus"] = result.HealthStatus.ToString(),
                ["latencyMs"] = result.LatencyMs ?? 0,
                ["isConnected"] = result.IsConnected
            });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Changes circuit breaker state with logging and event notification.
        /// </summary>
        private void ChangeCircuitState(CircuitBreakerState newState)
        {
            var oldState = _circuitState;
            _circuitState = newState;

            if (newState == CircuitBreakerState.Open)
            {
                _circuitOpenedAt = DateTime.UtcNow;
            }

            _logger.LogWarning(
                "Circuit breaker state changed: {OldState} → {NewState}",
                oldState, newState);

            CircuitBreakerStateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                Timestamp = DateTime.UtcNow
            });

            _structuredLogger.LogOperationalMetrics("MqttHealthMonitor", "CircuitBreakerStateChanged", new System.Collections.Generic.Dictionary<string, object>
            {
                ["oldState"] = oldState.ToString(),
                ["newState"] = newState.ToString(),
                ["consecutiveFailures"] = _consecutiveFailures
            });
        }

        /// <summary>
        /// Adds health check result to history.
        /// </summary>
        private void AddToHistory(HealthCheckResult result)
        {
            _recentHealthChecks.Enqueue(result);

            // Keep only last N results
            while (_recentHealthChecks.Count > _maxHealthCheckHistory)
            {
                _recentHealthChecks.TryDequeue(out _);
            }
        }

        /// <summary>
        /// Gets current circuit breaker state.
        /// </summary>
        public CircuitBreakerState GetCircuitState()
        {
            lock (_circuitLock)
            {
                // Auto-transition from OPEN to HALF_OPEN after timeout
                if (_circuitState == CircuitBreakerState.Open)
                {
                    var elapsedSinceOpen = DateTime.UtcNow - _circuitOpenedAt;
                    if (elapsedSinceOpen.TotalSeconds >= _settings.CircuitBreakerTimeoutSeconds)
                    {
                        ChangeCircuitState(CircuitBreakerState.HalfOpen);
                        _logger.LogInformation("Circuit breaker transitioning to HALF_OPEN for retry.");
                    }
                }

                return _circuitState;
            }
        }

        /// <summary>
        /// Checks if operations should be allowed based on circuit breaker state.
        /// </summary>
        public bool IsOperationAllowed()
        {
            var state = GetCircuitState();
            return state != CircuitBreakerState.Open;
        }

        /// <summary>
        /// Gets health statistics.
        /// </summary>
        public HealthStatistics GetHealthStatistics()
        {
            var total = Interlocked.Read(ref _totalHealthChecks);
            var success = Interlocked.Read(ref _successfulHealthChecks);
            var failed = Interlocked.Read(ref _failedHealthChecks);

            var recentResults = _recentHealthChecks.ToArray();
            var recentLatencies = new System.Collections.Generic.List<long>();

            foreach (var result in recentResults)
            {
                if (result.LatencyMs.HasValue)
                    recentLatencies.Add(result.LatencyMs.Value);
            }

            long avgLatency = 0;
            if (recentLatencies.Count > 0)
            {
                avgLatency = (long)recentLatencies.Average();
            }

            return new HealthStatistics
            {
                TotalHealthChecks = total,
                SuccessfulHealthChecks = success,
                FailedHealthChecks = failed,
                SuccessRate = total > 0 ? (double)success / total * 100 : 0,
                AverageLatencyMs = avgLatency,
                CircuitState = GetCircuitState(),
                ConsecutiveFailures = _consecutiveFailures,
                IsConnected = _mqttClient.IsConnected
            };
        }

        public void Dispose()
        {
            StopMonitoringAsync().GetAwaiter().GetResult();
            _healthCheckCts?.Dispose();
        }
    }

    // ==================================================================================
    // ENUMS & MODELS
    // ==================================================================================

    public enum CircuitBreakerState
    {
        Closed,   // Normal operation
        Open,     // Failures exceeded threshold, blocking operations
        HalfOpen  // Testing if service recovered
    }

    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    public class HealthCheckResult
    {
        public DateTime Timestamp { get; set; }
        public HealthStatus HealthStatus { get; set; }
        public bool IsConnected { get; set; }
        public long? LatencyMs { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    public class HealthStatistics
    {
        public long TotalHealthChecks { get; set; }
        public long SuccessfulHealthChecks { get; set; }
        public long FailedHealthChecks { get; set; }
        public double SuccessRate { get; set; }
        public long AverageLatencyMs { get; set; }
        public CircuitBreakerState CircuitState { get; set; }
        public int ConsecutiveFailures { get; set; }
        public bool IsConnected { get; set; }
    }

    public class CircuitBreakerStateChangedEventArgs : EventArgs
    {
        public CircuitBreakerState OldState { get; set; }
        public CircuitBreakerState NewState { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class HealthCheckFailedEventArgs : EventArgs
    {
        public HealthCheckResult Result { get; set; } = null!;
        public int ConsecutiveFailures { get; set; }
    }
}
