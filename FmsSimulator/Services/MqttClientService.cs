using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using FmsSimulator.Models;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace FmsSimulator.Services;

/// <summary>
/// Phase 1: Production MQTT Client Service
/// Manages real MQTT broker connectivity with high-availability features
/// </summary>
public class MqttClientService : IAsyncDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly MqttBrokerSettings _settings;
    private readonly Vda5050TopicSettings _topicSettings;
    private readonly ILogger<MqttClientService> _logger;
    private readonly LoggingService _structuredLogger = LoggingService.Instance;
    
    // Connection state
    private bool _isConnected = false;
    private int _reconnectionAttempts = 0;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    
    // Message persistence
    private readonly ConcurrentQueue<PendingMessage> _pendingMessages = new();
    private readonly int _maxPendingMessages = 10000;
    
    // Event handlers for received messages
    private readonly ConcurrentDictionary<string, List<Func<MqttApplicationMessage, Task>>> _messageHandlers = new();

    public MqttClientService(
        IOptions<MqttBrokerSettings> settings,
        IOptions<Vda5050TopicSettings> topicSettings,
        ILogger<MqttClientService> logger)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _topicSettings = topicSettings.Value ?? throw new ArgumentNullException(nameof(topicSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create MQTT client
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // Set up event handlers
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    /// <summary>
    /// Connect to MQTT broker
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected)
            {
                _logger.LogInformation("Already connected to MQTT broker");
                return;
            }

            _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port}...", 
                _settings.BrokerHost, _settings.BrokerPort);

            var options = BuildMqttClientOptions();
            await _mqttClient.ConnectAsync(options, cancellationToken);

            _isConnected = true;
            _reconnectionAttempts = 0;

            _logger.LogInformation("Successfully connected to MQTT broker");
            
            _structuredLogger.LogOperationalMetrics("MqttClient", "Connected", new Dictionary<string, object>
            {
                ["broker"] = _settings.BrokerHost,
                ["port"] = _settings.BrokerPort,
                ["clientId"] = _settings.ClientId
            });

            // Process any pending messages
            await ProcessPendingMessagesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MQTT broker");
            _structuredLogger.LogError("MqttClient", "ConnectionFailed", ex);
            
            if (_settings.AutoReconnect)
            {
                await ScheduleReconnectionAsync(cancellationToken);
            }
            else
            {
                throw;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Disconnect from MQTT broker
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isConnected)
                return;

            _logger.LogInformation("Disconnecting from MQTT broker...");
            
            var options = new MqttClientDisconnectOptions
            {
                Reason = MqttClientDisconnectOptionsReason.NormalDisconnection
            };
            
            await _mqttClient.DisconnectAsync(options, cancellationToken);
            _isConnected = false;

            _logger.LogInformation("Disconnected from MQTT broker");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MQTT disconnect");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Publish message to MQTT broker
    /// </summary>
    public async Task<bool> PublishAsync(
        string topic,
        string payload,
        MqttQualityOfServiceLevel? qos = null,
        bool? retain = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isConnected)
            {
                _logger.LogWarning("Not connected to broker. Queueing message for topic: {Topic}", topic);
                QueuePendingMessage(topic, payload, qos, retain);
                
                // Try to reconnect
                if (_settings.AutoReconnect)
                {
                    _ = ConnectAsync(cancellationToken);
                }
                
                return false;
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos ?? _settings.DefaultQoS)
                .WithRetainFlag(retain ?? _settings.RetainMessages)
                .Build();

            var result = await _mqttClient.PublishAsync(message, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogDebug("Published to {Topic}: {PayloadLength} bytes", topic, payload.Length);
                
                _structuredLogger.LogOperationalMetrics("MqttClient", "MessagePublished", new Dictionary<string, object>
                {
                    ["topic"] = topic,
                    ["payloadSize"] = payload.Length,
                    ["qos"] = qos ?? _settings.DefaultQoS,
                    ["reasonCode"] = result.ReasonCode.ToString()
                });
                
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to publish to {Topic}: {ReasonCode}", topic, result.ReasonCode);
                QueuePendingMessage(topic, payload, qos, retain);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing to topic {Topic}", topic);
            QueuePendingMessage(topic, payload, qos, retain);
            return false;
        }
    }

    /// <summary>
    /// Subscribe to topic
    /// </summary>
    public async Task<bool> SubscribeAsync(
        string topic,
        Func<MqttApplicationMessage, Task> messageHandler,
        MqttQualityOfServiceLevel? qos = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isConnected)
            {
                _logger.LogWarning("Not connected. Cannot subscribe to {Topic}", topic);
                return false;
            }

            // Register message handler
            _messageHandlers.AddOrUpdate(
                topic,
                _ => new List<Func<MqttApplicationMessage, Task>> { messageHandler },
                (_, list) =>
                {
                    list.Add(messageHandler);
                    return list;
                });

            var options = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel(qos ?? _settings.DefaultQoS))
                .Build();

            var result = await _mqttClient.SubscribeAsync(options, cancellationToken);

            if (result.Items.Any(i => i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS0 ||
                                     i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS1 ||
                                     i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS2))
            {
                _logger.LogInformation("Subscribed to topic: {Topic}", topic);
                
                _structuredLogger.LogOperationalMetrics("MqttClient", "Subscribed", new Dictionary<string, object>
                {
                    ["topic"] = topic,
                    ["qos"] = qos ?? _settings.DefaultQoS
                });
                
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to subscribe to {Topic}: {ResultCode}", 
                    topic, result.Items.FirstOrDefault()?.ResultCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to topic {Topic}", topic);
            return false;
        }
    }

    /// <summary>
    /// Unsubscribe from topic
    /// </summary>
    public async Task<bool> UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isConnected)
                return false;

            var options = new MqttClientUnsubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();

            await _mqttClient.UnsubscribeAsync(options, cancellationToken);
            
            _messageHandlers.TryRemove(topic, out _);
            
            _logger.LogInformation("Unsubscribed from topic: {Topic}", topic);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from topic {Topic}", topic);
            return false;
        }
    }

    /// <summary>
    /// Check if connected to broker
    /// </summary>
    public bool IsConnected => _isConnected && _mqttClient.IsConnected;

    /// <summary>
    /// Get number of pending messages
    /// </summary>
    public int PendingMessageCount => _pendingMessages.Count;

    // Private helper methods

    private MqttClientOptions BuildMqttClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(_settings.ClientId)
            .WithTcpServer(_settings.BrokerHost, _settings.BrokerPort)
            .WithCleanSession(_settings.CleanSession)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_settings.KeepAliveSeconds))
            .WithTimeout(TimeSpan.FromSeconds(_settings.ConnectionTimeoutSeconds));

        if (_settings.UseTls)
        {
            builder.WithTcpServer(_settings.BrokerHost, _settings.TlsPort)
                   .WithTlsOptions(o => o.UseTls());
        }

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            builder.WithCredentials(_settings.Username, _settings.Password);
        }

        // Note: Automatic reconnection is handled by MQTTnet v4 internally
        // We also implement our own reconnection logic in OnDisconnectedAsync

        return builder.Build();
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _isConnected = true;
        _reconnectionAttempts = 0;
        
        _logger.LogInformation("MQTT client connected. Session: {SessionPresent}, Result: {ResultCode}",
            args.ConnectResult.IsSessionPresent, args.ConnectResult.ResultCode);

        // Resubscribe to topics
        foreach (var topic in _messageHandlers.Keys)
        {
            try
            {
                await SubscribeAsync(topic, _ => Task.CompletedTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resubscribe to {Topic}", topic);
            }
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _isConnected = false;
        
        _logger.LogWarning("MQTT client disconnected. Reason: {Reason}", args.Reason);

        if (_settings.AutoReconnect && args.ClientWasConnected)
        {
            await ScheduleReconnectionAsync();
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            _logger.LogDebug("Received message on topic {Topic}: {PayloadLength} bytes", 
                topic, payload.Length);

            // Find matching handlers (exact match or wildcard)
            var matchingHandlers = _messageHandlers
                .Where(kvp => TopicMatches(topic, kvp.Key))
                .SelectMany(kvp => kvp.Value)
                .ToList();

            if (matchingHandlers.Any())
            {
                foreach (var handler in matchingHandlers)
                {
                    try
                    {
                        await handler(args.ApplicationMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in message handler for topic {Topic}", topic);
                    }
                }
            }
            else
            {
                _logger.LogDebug("No handler registered for topic {Topic}", topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received message");
        }
    }

    private bool TopicMatches(string actualTopic, string filterTopic)
    {
        // Simple wildcard matching for MQTT topics
        if (filterTopic == actualTopic)
            return true;

        var actualParts = actualTopic.Split('/');
        var filterParts = filterTopic.Split('/');

        if (filterParts.Length > actualParts.Length)
            return false;

        for (int i = 0; i < filterParts.Length; i++)
        {
            if (filterParts[i] == "#")
                return true; // Multi-level wildcard

            if (filterParts[i] == "+")
                continue; // Single-level wildcard

            if (filterParts[i] != actualParts[i])
                return false;
        }

        return filterParts.Length == actualParts.Length;
    }

    private async Task ScheduleReconnectionAsync(CancellationToken cancellationToken = default)
    {
        _reconnectionAttempts++;

        if (_settings.MaxReconnectAttempts > 0 && _reconnectionAttempts > _settings.MaxReconnectAttempts)
        {
            _logger.LogError("Max reconnection attempts ({MaxAttempts}) reached. Giving up.", 
                _settings.MaxReconnectAttempts);
            return;
        }

        var delay = TimeSpan.FromSeconds(_settings.ReconnectDelaySeconds);
        
        _logger.LogInformation("Scheduling reconnection attempt {Attempt} in {Delay} seconds...",
            _reconnectionAttempts, delay.TotalSeconds);

        await Task.Delay(delay, cancellationToken);

        try
        {
            await ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reconnection attempt {Attempt} failed", _reconnectionAttempts);
        }
    }

    private void QueuePendingMessage(
        string topic,
        string payload,
        MqttQualityOfServiceLevel? qos,
        bool? retain)
    {
        if (_pendingMessages.Count >= _maxPendingMessages)
        {
            _logger.LogWarning("Pending message queue full. Dropping oldest message.");
            _pendingMessages.TryDequeue(out _);
        }

        _pendingMessages.Enqueue(new PendingMessage
        {
            Topic = topic,
            Payload = payload,
            QoS = qos ?? _settings.DefaultQoS,
            Retain = retain ?? _settings.RetainMessages,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogDebug("Queued message for topic {Topic}. Queue size: {QueueSize}", 
            topic, _pendingMessages.Count);
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var processedCount = 0;
        var failedCount = 0;

        while (_pendingMessages.TryDequeue(out var message))
        {
            try
            {
                var success = await PublishAsync(
                    message.Topic,
                    message.Payload,
                    message.QoS,
                    message.Retain,
                    cancellationToken);

                if (success)
                    processedCount++;
                else
                    failedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending message for topic {Topic}", message.Topic);
                failedCount++;
            }
        }

        if (processedCount > 0 || failedCount > 0)
        {
            _logger.LogInformation("Processed pending messages: {Processed} succeeded, {Failed} failed",
                processedCount, failedCount);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _mqttClient?.Dispose();
        _connectionLock?.Dispose();
    }

    private record PendingMessage
    {
        public string Topic { get; init; } = "";
        public string Payload { get; init; } = "";
        public MqttQualityOfServiceLevel QoS { get; init; }
        public bool Retain { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
