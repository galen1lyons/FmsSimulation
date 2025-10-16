using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using FmsSimulator.Models;
using FmsSimulator.Services;

namespace FmsSimulator.Services.MQTT
{
    /// <summary>
    /// VDA 5050 Subscriber Service - Subscribes to VDA 5050 protocol messages from MQTT.
    /// Receives state updates, visualization data, and connection status from AGVs.
    /// 
    /// Key Responsibilities:
    /// - Subscribe to VDA 5050 state topics (state, visualization, connection)
    /// - Parse incoming JSON messages to VDA 5050 models
    /// - Raise events for state changes to notify other services
    /// - Track AGV connection status
    /// - Handle message deserialization errors gracefully
    /// </summary>
    public class Vda5050SubscriberService
    {
        private readonly MqttClientService _mqttClient;
        private readonly Vda5050TopicSettings _topicSettings;
        private readonly ILogger<Vda5050SubscriberService> _logger;

        private readonly Dictionary<string, DateTime> _lastStateReceived = new();
        private readonly object _stateLock = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Events for state updates
        public event EventHandler<StateMessageReceivedEventArgs>? StateReceived;
        public event EventHandler<VisualizationMessageReceivedEventArgs>? VisualizationReceived;
        public event EventHandler<ConnectionMessageReceivedEventArgs>? ConnectionStateChanged;

        public Vda5050SubscriberService(
            MqttClientService mqttClient,
            IOptions<MqttSettings> mqttSettings,
            ILogger<Vda5050SubscriberService> logger)
        {
            _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
            _topicSettings = mqttSettings.Value?.Vda5050Topics ?? throw new ArgumentNullException(nameof(mqttSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts subscribing to all VDA 5050 topics for the specified AGVs.
        /// </summary>
        /// <param name="agvIds">List of AGV IDs to monitor. If null/empty, subscribes to all AGVs using wildcard.</param>
        public async Task StartSubscribingAsync(List<string>? agvIds = null)
        {
            try
            {
                if (agvIds == null || agvIds.Count == 0)
                {
                    // Subscribe to all AGVs using wildcard
                    await SubscribeToAllAgvsAsync();
                }
                else
                {
                    // Subscribe to specific AGVs
                    foreach (var agvId in agvIds)
                    {
                        await SubscribeToAgvAsync(agvId);
                    }
                }

                _logger.LogInformation(
                    "VDA 5050 Subscriber started. Monitoring {AgvCount} AGV(s).",
                    agvIds?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start VDA 5050 Subscriber.");
                throw;
            }
        }

        /// <summary>
        /// Subscribes to VDA 5050 topics for a specific AGV.
        /// </summary>
        public async Task SubscribeToAgvAsync(string agvId)
        {
            if (string.IsNullOrWhiteSpace(agvId))
                throw new ArgumentException("AGV ID cannot be null or empty.", nameof(agvId));

            try
            {
                // Subscribe to state topic
                string stateTopic = _topicSettings.GetStateTopic(agvId);
                await _mqttClient.SubscribeAsync(stateTopic, OnMqttMessageReceivedAsync);
                _logger.LogInformation("Subscribed to VDA 5050 state topic: {Topic}", stateTopic);

                // Subscribe to visualization topic
                string visualizationTopic = _topicSettings.GetVisualizationTopic(agvId);
                await _mqttClient.SubscribeAsync(visualizationTopic, OnMqttMessageReceivedAsync);
                _logger.LogInformation("Subscribed to VDA 5050 visualization topic: {Topic}", visualizationTopic);

                // Subscribe to connection topic
                string connectionTopic = _topicSettings.GetConnectionTopic(agvId);
                await _mqttClient.SubscribeAsync(connectionTopic, OnMqttMessageReceivedAsync);
                _logger.LogInformation("Subscribed to VDA 5050 connection topic: {Topic}", connectionTopic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to VDA 5050 topics for AGV {AgvId}.", agvId);
                throw;
            }
        }

        /// <summary>
        /// Subscribes to all AGV topics using wildcard subscriptions.
        /// </summary>
        private async Task SubscribeToAllAgvsAsync()
        {
            // Subscribe to state wildcard
            string stateWildcard = _topicSettings.GetStateWildcardTopic();
            await _mqttClient.SubscribeAsync(stateWildcard, OnMqttMessageReceivedAsync);
            _logger.LogInformation("Subscribed to VDA 5050 state wildcard: {Topic}", stateWildcard);

            // Subscribe to visualization wildcard
            string visualizationWildcard = $"{_topicSettings.BaseTopicPrefix}/{_topicSettings.Manufacturer}/+/visualization";
            await _mqttClient.SubscribeAsync(visualizationWildcard, OnMqttMessageReceivedAsync);
            _logger.LogInformation("Subscribed to VDA 5050 visualization wildcard: {Topic}", visualizationWildcard);

            // Subscribe to connection wildcard
            string connectionWildcard = $"{_topicSettings.BaseTopicPrefix}/{_topicSettings.Manufacturer}/+/connection";
            await _mqttClient.SubscribeAsync(connectionWildcard, OnMqttMessageReceivedAsync);
            _logger.LogInformation("Subscribed to VDA 5050 connection wildcard: {Topic}", connectionWildcard);
        }

        /// <summary>
        /// Unsubscribes from all topics for a specific AGV.
        /// </summary>
        public async Task UnsubscribeFromAgvAsync(string agvId)
        {
            if (string.IsNullOrWhiteSpace(agvId))
                throw new ArgumentException("AGV ID cannot be null or empty.", nameof(agvId));

            try
            {
                await _mqttClient.UnsubscribeAsync(_topicSettings.GetStateTopic(agvId));
                await _mqttClient.UnsubscribeAsync(_topicSettings.GetVisualizationTopic(agvId));
                await _mqttClient.UnsubscribeAsync(_topicSettings.GetConnectionTopic(agvId));

                _logger.LogInformation("Unsubscribed from VDA 5050 topics for AGV {AgvId}.", agvId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from VDA 5050 topics for AGV {AgvId}.", agvId);
                throw;
            }
        }

        /// <summary>
        /// Handles incoming MQTT messages and routes them to appropriate handlers.
        /// </summary>
        private async Task OnMqttMessageReceivedAsync(MqttApplicationMessage message)
        {
            try
            {
                string topic = message.Topic;
                string payload = Encoding.UTF8.GetString(message.PayloadSegment);

                // Determine message type from topic
                if (topic.EndsWith("/state"))
                {
                    HandleStateMessage(topic, payload);
                }
                else if (topic.EndsWith("/visualization"))
                {
                    HandleVisualizationMessage(topic, payload);
                }
                else if (topic.EndsWith("/connection"))
                {
                    HandleConnectionMessage(topic, payload);
                }
                else
                {
                    _logger.LogWarning("Received message on unknown VDA 5050 topic: {Topic}", topic);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message from topic: {Topic}", message.Topic);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles VDA 5050 State messages.
        /// </summary>
        private void HandleStateMessage(string topic, string payload)
        {
            try
            {
                var stateMessage = JsonSerializer.Deserialize<StateMessage>(payload, _jsonOptions);
                if (stateMessage == null)
                {
                    _logger.LogWarning("Failed to deserialize StateMessage from topic: {Topic}", topic);
                    return;
                }

                // Extract AGV ID from topic
                string agvId = ExtractAgvIdFromTopic(topic);

                // Update last received timestamp
                lock (_stateLock)
                {
                    _lastStateReceived[agvId] = DateTime.UtcNow;
                }

                // Raise event
                StateReceived?.Invoke(this, new StateMessageReceivedEventArgs
                {
                    AgvId = agvId,
                    Topic = topic,
                    StateMessage = stateMessage,
                    ReceivedAt = DateTime.UtcNow
                });

                _logger.LogDebug(
                    "Received VDA 5050 State from AGV {AgvId}: OrderId={OrderId}, LastNode={LastNode}, Driving={Driving}, Battery={Battery}%",
                    agvId, stateMessage.OrderId, stateMessage.LastNodeId, stateMessage.Driving, stateMessage.BatteryState?.BatteryCharge ?? 0);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error for StateMessage. Topic: {Topic}, Payload: {Payload}", topic, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling StateMessage from topic: {Topic}", topic);
            }
        }

        /// <summary>
        /// Handles VDA 5050 Visualization messages.
        /// </summary>
        private void HandleVisualizationMessage(string topic, string payload)
        {
            try
            {
                var visualizationMessage = JsonSerializer.Deserialize<VisualizationMessage>(payload, _jsonOptions);
                if (visualizationMessage == null)
                {
                    _logger.LogWarning("Failed to deserialize VisualizationMessage from topic: {Topic}", topic);
                    return;
                }

                string agvId = ExtractAgvIdFromTopic(topic);

                // Raise event
                VisualizationReceived?.Invoke(this, new VisualizationMessageReceivedEventArgs
                {
                    AgvId = agvId,
                    Topic = topic,
                    VisualizationMessage = visualizationMessage,
                    ReceivedAt = DateTime.UtcNow
                });

                _logger.LogDebug(
                    "Received VDA 5050 Visualization from AGV {AgvId}: Position=({X}, {Y}, {Theta})",
                    agvId,
                    visualizationMessage.AgvPosition?.X ?? 0,
                    visualizationMessage.AgvPosition?.Y ?? 0,
                    visualizationMessage.AgvPosition?.Theta ?? 0);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error for VisualizationMessage. Topic: {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling VisualizationMessage from topic: {Topic}", topic);
            }
        }

        /// <summary>
        /// Handles VDA 5050 Connection messages.
        /// </summary>
        private void HandleConnectionMessage(string topic, string payload)
        {
            try
            {
                var connectionMessage = JsonSerializer.Deserialize<ConnectionMessage>(payload, _jsonOptions);
                if (connectionMessage == null)
                {
                    _logger.LogWarning("Failed to deserialize ConnectionMessage from topic: {Topic}", topic);
                    return;
                }

                string agvId = ExtractAgvIdFromTopic(topic);

                // Raise event
                ConnectionStateChanged?.Invoke(this, new ConnectionMessageReceivedEventArgs
                {
                    AgvId = agvId,
                    Topic = topic,
                    ConnectionMessage = connectionMessage,
                    ReceivedAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "AGV {AgvId} connection state changed: {ConnectionState}",
                    agvId, connectionMessage.ConnectionState);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error for ConnectionMessage. Topic: {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ConnectionMessage from topic: {Topic}", topic);
            }
        }

        /// <summary>
        /// Extracts the AGV ID from the topic string.
        /// Example: "vda5050/v2/FMS/AGV_001/state" -> "AGV_001"
        /// </summary>
        private string ExtractAgvIdFromTopic(string topic)
        {
            var parts = topic.Split('/');
            if (parts.Length >= 4)
            {
                return parts[3]; // AGV ID is the 4th segment
            }
            return "UNKNOWN";
        }

        /// <summary>
        /// Gets the last received timestamp for an AGV.
        /// </summary>
        public DateTime? GetLastStateReceived(string agvId)
        {
            lock (_stateLock)
            {
                return _lastStateReceived.TryGetValue(agvId, out var timestamp) ? timestamp : null;
            }
        }

        /// <summary>
        /// Checks if an AGV is considered online based on last state message.
        /// </summary>
        public bool IsAgvOnline(string agvId, TimeSpan timeout)
        {
            var lastReceived = GetLastStateReceived(agvId);
            if (!lastReceived.HasValue)
                return false;

            return DateTime.UtcNow - lastReceived.Value < timeout;
        }
    }

    // ==================================================================================
    // EVENT ARGS
    // ==================================================================================

    public class StateMessageReceivedEventArgs : EventArgs
    {
        public string AgvId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public StateMessage StateMessage { get; set; } = null!;
        public DateTime ReceivedAt { get; set; }
    }

    public class VisualizationMessageReceivedEventArgs : EventArgs
    {
        public string AgvId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public VisualizationMessage VisualizationMessage { get; set; } = null!;
        public DateTime ReceivedAt { get; set; }
    }

    public class ConnectionMessageReceivedEventArgs : EventArgs
    {
        public string AgvId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public ConnectionMessage ConnectionMessage { get; set; } = null!;
        public DateTime ReceivedAt { get; set; }
    }
}
