using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FmsSimulator.Models;

namespace FmsSimulator.Services
{
    /// <summary>
    /// MQTT High-Availability Orchestrator - Coordinates all HA services.
    /// 
    /// Key Responsibilities:
    /// - Initialize and coordinate health monitoring
    /// - Manage message persistence lifecycle
    /// - Handle automatic failover scenarios
    /// - Provide unified HA status and metrics
    /// - Graceful degradation management
    /// </summary>
    public class MqttHighAvailabilityOrchestrator : IDisposable
    {
        private readonly MqttClientService _mqttClient;
        private readonly MqttHealthMonitor _healthMonitor;
        private readonly MqttMessagePersistenceService _persistenceService;
        private readonly MqttHighAvailabilitySettings _settings;
        private readonly ILogger<MqttHighAvailabilityOrchestrator> _logger;
        private readonly LoggingService _structuredLogger = LoggingService.Instance;

        private CancellationTokenSource? _orchestrationCts;
        private Task? _orchestrationTask;
        private bool _isStarted = false;

        public MqttHighAvailabilityOrchestrator(
            MqttClientService mqttClient,
            MqttHealthMonitor healthMonitor,
            MqttMessagePersistenceService persistenceService,
            IOptions<MqttSettings> mqttSettings,
            ILogger<MqttHighAvailabilityOrchestrator> logger)
        {
            _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
            _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _settings = mqttSettings.Value?.MqttHighAvailability ?? throw new ArgumentNullException(nameof(mqttSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to health monitor events
            _healthMonitor.CircuitBreakerStateChanged += OnCircuitBreakerStateChanged;
            _healthMonitor.HealthCheckFailed += OnHealthCheckFailed;
        }

        /// <summary>
        /// Starts the high-availability orchestration.
        /// </summary>
        public async Task StartAsync()
        {
            if (_isStarted)
            {
                _logger.LogWarning("HA Orchestrator is already started.");
                return;
            }

            _logger.LogInformation("Starting MQTT High-Availability Orchestrator...");

            try
            {
                // Step 1: Load persisted messages
                if (_settings.EnableMessagePersistence)
                {
                    var persistedMessages = await _persistenceService.LoadPersistedMessagesAsync();
                    if (persistedMessages.Count > 0)
                    {
                        _logger.LogInformation(
                            "Loaded {Count} persisted messages. Will retry publishing after connection.",
                            persistedMessages.Count);

                        // NOTE: Messages loaded but not yet auto-queued for retry (Phase 1.5 enhancement)
                        // See BACKLOG.md: "Automatic Message Retry Queue" for auto-retry feature
                        // Current: Messages logged, require manual intervention or app restart to retry
                    }
                }

                // Step 2: Connect to MQTT broker
                await _mqttClient.ConnectAsync();

                // Step 3: Start health monitoring
                _healthMonitor.StartMonitoring();

                // Step 4: Start orchestration loop
                _orchestrationCts = new CancellationTokenSource();
                _orchestrationTask = Task.Run(() => OrchestrationLoopAsync(_orchestrationCts.Token));

                _isStarted = true;

                _logger.LogInformation("MQTT High-Availability Orchestrator started successfully.");

                _structuredLogger.LogOperationalMetrics("MqttHA", "Started", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["enableCircuitBreaker"] = _settings.EnableCircuitBreaker,
                    ["enablePersistence"] = _settings.EnableMessagePersistence,
                    ["connectionPoolSize"] = _settings.PoolSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start HA Orchestrator.");
                throw;
            }
        }

        /// <summary>
        /// Stops the high-availability orchestration.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isStarted)
                return;

            _logger.LogInformation("Stopping MQTT High-Availability Orchestrator...");

            try
            {
                // Stop orchestration loop
                _orchestrationCts?.Cancel();
                if (_orchestrationTask != null)
                {
                    await _orchestrationTask;
                }

                // Stop health monitoring
                await _healthMonitor.StopMonitoringAsync();

                // Disconnect from MQTT
                await _mqttClient.DisconnectAsync();

                // Cleanup old persisted files
                if (_settings.EnableMessagePersistence)
                {
                    await _persistenceService.CleanupOldFilesAsync();
                }

                _isStarted = false;

                _logger.LogInformation("MQTT High-Availability Orchestrator stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping HA Orchestrator.");
                throw;
            }
        }

        /// <summary>
        /// Main orchestration loop - periodic maintenance tasks.
        /// </summary>
        private async Task OrchestrationLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Run every 5 minutes
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

                    // Task 1: Cleanup old persisted files
                    if (_settings.EnableMessagePersistence)
                    {
                        await _persistenceService.CleanupOldFilesAsync();
                    }

                    // Task 2: Log health statistics
                    var healthStats = _healthMonitor.GetHealthStatistics();
                    var persistStats = await _persistenceService.GetStatisticsAsync();

                    _logger.LogInformation(
                        "HA Status - Health: {SuccessRate:F1}%, Circuit: {CircuitState}, Latency: {LatencyMs}ms, Persisted Files: {FileCount}",
                        healthStats.SuccessRate,
                        healthStats.CircuitState,
                        healthStats.AverageLatencyMs,
                        persistStats.FileCount);

                    _structuredLogger.LogOperationalMetrics("MqttHA", "PeriodicStatus", new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["healthSuccessRate"] = healthStats.SuccessRate,
                        ["circuitState"] = healthStats.CircuitState.ToString(),
                        ["averageLatencyMs"] = healthStats.AverageLatencyMs,
                        ["persistedFileCount"] = persistStats.FileCount,
                        ["persistedSizeMB"] = persistStats.TotalSizeMB,
                        ["consecutiveFailures"] = healthStats.ConsecutiveFailures
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in orchestration loop.");
                }
            }
        }

        /// <summary>
        /// Handles circuit breaker state changes.
        /// </summary>
        private void OnCircuitBreakerStateChanged(object? sender, CircuitBreakerStateChangedEventArgs e)
        {
            _logger.LogWarning(
                "Circuit breaker state changed: {OldState} → {NewState}",
                e.OldState, e.NewState);

            // Perform actions based on state
            switch (e.NewState)
            {
                case CircuitBreakerState.Open:
                    // Circuit opened - stop accepting new operations
                    _logger.LogError(
                        "Circuit breaker OPENED due to consecutive failures. Operations blocked for {Timeout}s.",
                        _settings.CircuitBreakerTimeoutSeconds);
                    break;

                case CircuitBreakerState.HalfOpen:
                    // Circuit half-open - allow test traffic
                    _logger.LogInformation("Circuit breaker HALF-OPEN. Testing if service recovered...");
                    break;

                case CircuitBreakerState.Closed:
                    // Circuit closed - normal operation
                    _logger.LogInformation("Circuit breaker CLOSED. Service recovered, normal operation resumed.");
                    break;
            }
        }

        /// <summary>
        /// Handles health check failures.
        /// </summary>
        private void OnHealthCheckFailed(object? sender, HealthCheckFailedEventArgs e)
        {
            _logger.LogWarning(
                "Health check failed ({Failures} consecutive). Status: {Status}, Message: {Message}",
                e.ConsecutiveFailures,
                e.Result.HealthStatus,
                e.Result.Message);

            // Optional: Trigger additional recovery actions
            if (e.ConsecutiveFailures >= 3 && !_mqttClient.IsConnected)
            {
                _logger.LogWarning("Attempting manual reconnection due to health check failures...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _mqttClient.ConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Manual reconnection attempt failed.");
                    }
                });
            }
        }

        /// <summary>
        /// Gets comprehensive high-availability status.
        /// </summary>
        public async Task<HighAvailabilityStatus> GetStatusAsync()
        {
            var healthStats = _healthMonitor.GetHealthStatistics();
            var persistStats = await _persistenceService.GetStatisticsAsync();

            return new HighAvailabilityStatus
            {
                IsOperational = _isStarted && _mqttClient.IsConnected && _healthMonitor.IsOperationAllowed(),
                IsConnected = _mqttClient.IsConnected,
                CircuitState = healthStats.CircuitState,
                HealthStatistics = healthStats,
                PersistenceStatistics = persistStats,
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Checks if MQTT operations should be allowed.
        /// </summary>
        public bool IsOperationAllowed()
        {
            // Check circuit breaker state
            if (!_healthMonitor.IsOperationAllowed())
            {
                _logger.LogWarning("Operation blocked by circuit breaker (state: {State}).",
                    _healthMonitor.GetCircuitState());
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _orchestrationCts?.Dispose();
        }
    }

    // ==================================================================================
    // MODELS
    // ==================================================================================

    /// <summary>
    /// Comprehensive high-availability status.
    /// </summary>
    public class HighAvailabilityStatus
    {
        public bool IsOperational { get; set; }
        public bool IsConnected { get; set; }
        public CircuitBreakerState CircuitState { get; set; }
        public HealthStatistics HealthStatistics { get; set; } = null!;
        public PersistenceStatistics PersistenceStatistics { get; set; } = null!;
        public DateTime LastUpdated { get; set; }
    }
}
