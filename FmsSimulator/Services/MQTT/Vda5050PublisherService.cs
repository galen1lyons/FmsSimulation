using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FmsSimulator.Models;
using FmsSimulator.Services;

namespace FmsSimulator.Services.MQTT
{
    /// <summary>
    /// VDA 5050 Publisher Service - Publishes VDA 5050 protocol messages to MQTT.
    /// Wraps MqttClientService to provide VDA 5050-specific publishing functionality.
    /// 
    /// Key Responsibilities:
    /// - Publish Order messages (mission assignments to AGVs)
    /// - Publish InstantActions messages (immediate commands like emergency stop)
    /// - Enforce VDA 5050 topic structure
    /// - Serialize messages to JSON with VDA 5050 compliance
    /// - Track message sequence (headerId, orderUpdateId)
    /// </summary>
    public class Vda5050PublisherService
    {
        private readonly MqttClientService _mqttClient;
        private readonly Vda5050TopicSettings _topicSettings;
        private readonly ILogger<Vda5050PublisherService> _logger;

        private int _nextHeaderId = 1;
        private readonly object _headerIdLock = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public Vda5050PublisherService(
            MqttClientService mqttClient,
            IOptions<MqttSettings> mqttSettings,
            ILogger<Vda5050PublisherService> logger)
        {
            _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
            _topicSettings = mqttSettings.Value?.Vda5050Topics ?? throw new ArgumentNullException(nameof(mqttSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Publishes a VDA 5050 Order message to the specified AGV.
        /// </summary>
        /// <param name="order">The order message containing nodes, edges, and actions.</param>
        /// <param name="agvId">The AGV identifier (serial number).</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        public async Task<bool> PublishOrderAsync(OrderMessage order, string agvId)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (string.IsNullOrWhiteSpace(agvId))
                throw new ArgumentException("AGV ID cannot be null or empty.", nameof(agvId));

            try
            {
                // Assign header ID if not set
                if (order.HeaderId == 0)
                {
                    order.HeaderId = GetNextHeaderId();
                }

                // Ensure timestamp is current
                order.Timestamp = DateTime.UtcNow.ToString("o");

                // Set manufacturer from configuration if not set
                if (string.IsNullOrWhiteSpace(order.Manufacturer))
                {
                    order.Manufacturer = _topicSettings.Manufacturer;
                }

                // Serialize to JSON
                string payload = JsonSerializer.Serialize(order, _jsonOptions);

                // Build topic
                string topic = _topicSettings.GetOrderTopic(agvId);

                // Publish to MQTT
                bool success = await _mqttClient.PublishAsync(topic, payload);

                if (success)
                {
                    _logger.LogInformation(
                        "Published VDA 5050 Order to AGV {AgvId}. OrderId: {OrderId}, OrderUpdateId: {OrderUpdateId}, HeaderId: {HeaderId}, Nodes: {NodeCount}, Edges: {EdgeCount}",
                        agvId, order.OrderId, order.OrderUpdateId, order.HeaderId, order.Nodes?.Count ?? 0, order.Edges?.Count ?? 0);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to publish VDA 5050 Order to AGV {AgvId}. OrderId: {OrderId}",
                        agvId, order.OrderId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception while publishing VDA 5050 Order to AGV {AgvId}. OrderId: {OrderId}",
                    agvId, order?.OrderId);
                return false;
            }
        }

        /// <summary>
        /// Publishes a VDA 5050 InstantActions message to the specified AGV.
        /// Instant actions are executed immediately (e.g., emergency stop, pause, cancel order).
        /// </summary>
        /// <param name="instantActions">The instant actions message.</param>
        /// <param name="agvId">The AGV identifier (serial number).</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        public async Task<bool> PublishInstantActionsAsync(InstantActionsMessage instantActions, string agvId)
        {
            if (instantActions == null)
                throw new ArgumentNullException(nameof(instantActions));

            if (string.IsNullOrWhiteSpace(agvId))
                throw new ArgumentException("AGV ID cannot be null or empty.", nameof(agvId));

            try
            {
                // Assign header ID if not set
                if (instantActions.HeaderId == 0)
                {
                    instantActions.HeaderId = GetNextHeaderId();
                }

                // Ensure timestamp is current
                instantActions.Timestamp = DateTime.UtcNow.ToString("o");

                // Set manufacturer from configuration if not set
                if (string.IsNullOrWhiteSpace(instantActions.Manufacturer))
                {
                    instantActions.Manufacturer = _topicSettings.Manufacturer;
                }

                // Serialize to JSON
                string payload = JsonSerializer.Serialize(instantActions, _jsonOptions);

                // Build topic
                string topic = _topicSettings.GetInstantActionsTopic(agvId);

                // Publish to MQTT
                bool success = await _mqttClient.PublishAsync(topic, payload);

                if (success)
                {
                    _logger.LogInformation(
                        "Published VDA 5050 InstantActions to AGV {AgvId}. HeaderId: {HeaderId}, ActionCount: {ActionCount}",
                        agvId, instantActions.HeaderId, instantActions.InstantActions?.Count ?? 0);

                    // Log individual actions
                    if (instantActions.InstantActions != null)
                    {
                        foreach (var action in instantActions.InstantActions)
                        {
                            _logger.LogDebug(
                                "InstantAction - Type: {ActionType}, Id: {ActionId}, Blocking: {BlockingType}",
                                action.ActionType, action.ActionId, action.BlockingType);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to publish VDA 5050 InstantActions to AGV {AgvId}. HeaderId: {HeaderId}",
                        agvId, instantActions.HeaderId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception while publishing VDA 5050 InstantActions to AGV {AgvId}. HeaderId: {HeaderId}",
                    agvId, instantActions?.HeaderId);
                return false;
            }
        }

        /// <summary>
        /// Publishes an emergency stop instant action to the specified AGV.
        /// </summary>
        /// <param name="agvId">The AGV identifier (serial number).</param>
        /// <param name="actionId">Unique identifier for the emergency stop action.</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        public async Task<bool> PublishEmergencyStopAsync(string agvId, string? actionId = null)
        {
            var instantActions = new InstantActionsMessage
            {
                Manufacturer = _topicSettings.Manufacturer,
                SerialNumber = agvId,
                InstantActions = new()
                {
                    new Action
                    {
                        ActionType = "stopPause", // VDA 5050 standard action type
                        ActionId = actionId ?? Guid.NewGuid().ToString(),
                        BlockingType = BlockingType.HARD,
                        ActionDescription = "Emergency stop requested by FMS"
                    }
                }
            };

            return await PublishInstantActionsAsync(instantActions, agvId);
        }

        /// <summary>
        /// Publishes a cancel order instant action to the specified AGV.
        /// </summary>
        /// <param name="agvId">The AGV identifier (serial number).</param>
        /// <param name="actionId">Unique identifier for the cancel action.</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        public async Task<bool> PublishCancelOrderAsync(string agvId, string? actionId = null)
        {
            var instantActions = new InstantActionsMessage
            {
                Manufacturer = _topicSettings.Manufacturer,
                SerialNumber = agvId,
                InstantActions = new()
                {
                    new Action
                    {
                        ActionType = "cancelOrder", // VDA 5050 standard action type
                        ActionId = actionId ?? Guid.NewGuid().ToString(),
                        BlockingType = BlockingType.HARD,
                        ActionDescription = "Order cancellation requested by FMS"
                    }
                }
            };

            return await PublishInstantActionsAsync(instantActions, agvId);
        }

        /// <summary>
        /// Publishes a resume instant action to the specified AGV.
        /// </summary>
        /// <param name="agvId">The AGV identifier (serial number).</param>
        /// <param name="actionId">Unique identifier for the resume action.</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        public async Task<bool> PublishResumeAsync(string agvId, string? actionId = null)
        {
            var instantActions = new InstantActionsMessage
            {
                Manufacturer = _topicSettings.Manufacturer,
                SerialNumber = agvId,
                InstantActions = new()
                {
                    new Action
                    {
                        ActionType = "stopPause", // VDA 5050: stopPause with resumeState parameter
                        ActionId = actionId ?? Guid.NewGuid().ToString(),
                        BlockingType = BlockingType.NONE,
                        ActionDescription = "Resume operation requested by FMS",
                        ActionParameters = new()
                        {
                            new ActionParameter
                            {
                                Key = "resumeState",
                                Value = true
                            }
                        }
                    }
                }
            };

            return await PublishInstantActionsAsync(instantActions, agvId);
        }

        /// <summary>
        /// Generates the next header ID in a thread-safe manner.
        /// </summary>
        private int GetNextHeaderId()
        {
            lock (_headerIdLock)
            {
                int headerId = _nextHeaderId;
                _nextHeaderId++;
                if (_nextHeaderId > 2147483647) // int.MaxValue
                {
                    _nextHeaderId = 1; // Wrap around
                }
                return headerId;
            }
        }

        /// <summary>
        /// Builds a VDA 5050 Order message from FMS AssignmentPlan data.
        /// Helper method to convert internal plan representation to VDA 5050 format.
        /// </summary>
        public OrderMessage BuildOrderFromAssignmentPlan(AssignmentPlan plan, int orderUpdateId = 0)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            string orderId = $"order_{plan.AssignedAmr.Id}_{DateTime.UtcNow.Ticks}";
            string agvId = plan.AssignedAmr.Id;

            var order = new OrderMessage
            {
                Manufacturer = _topicSettings.Manufacturer,
                SerialNumber = agvId,
                OrderId = orderId,
                OrderUpdateId = orderUpdateId,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Nodes = new(),
                Edges = new()
            };

            // Node 0: Start location (from location)
            var startNode = new Node
            {
                NodeId = plan.Task.FromLocation,
                SequenceId = 0,
                Released = true,
                NodeDescription = $"Pick location: {plan.Task.FromLocation}",
                NodePosition = new NodePosition
                {
                    // NOTE: Placeholder coordinates - MapService implementation planned for Phase 2
                    // See BACKLOG.md: "MapService Implementation" for coordinate resolution feature
                    // AGVs should use NodeId (location name) for navigation until MapService is available
                    X = 0.0,
                    Y = 0.0,
                    Theta = 0.0,
                    MapId = "warehouse_map",
                    AllowedDeviationXy = 0.5,
                    AllowedDeviationTheta = 0.1
                },
                Actions = new()
                {
                    new Action
                    {
                        ActionType = "pick",
                        ActionId = $"{orderId}_pick",
                        BlockingType = BlockingType.HARD,
                        ActionDescription = $"Pick from: {plan.Task.FromLocation}"
                    }
                }
            };
            order.Nodes.Add(startNode);

            // Edge 1: Travel from start to destination
            var edge = new Edge
            {
                EdgeId = $"{plan.Task.FromLocation}_to_{plan.Task.ToLocation}",
                SequenceId = 1,
                Released = true,
                StartNodeId = plan.Task.FromLocation,
                EndNodeId = plan.Task.ToLocation,
                MaxSpeed = 2.0, // m/s
                OrientationType = OrientationType.TANGENTIAL,
                Actions = new()
            };
            order.Edges.Add(edge);

            // Node 2: End location (to location)
            var endNode = new Node
            {
                NodeId = plan.Task.ToLocation,
                SequenceId = 2,
                Released = true,
                NodeDescription = $"Drop location: {plan.Task.ToLocation}",
                NodePosition = new NodePosition
                {
                    // NOTE: Placeholder coordinates - MapService implementation planned for Phase 2
                    // See BACKLOG.md: "MapService Implementation" for coordinate resolution feature
                    // AGVs should use NodeId (location name) for navigation until MapService is available
                    X = 0.0,
                    Y = 0.0,
                    Theta = 0.0,
                    MapId = "warehouse_map",
                    AllowedDeviationXy = 0.5,
                    AllowedDeviationTheta = 0.1
                },
                Actions = new()
                {
                    new Action
                    {
                        ActionType = "drop",
                        ActionId = $"{orderId}_drop",
                        BlockingType = BlockingType.HARD,
                        ActionDescription = $"Drop at: {plan.Task.ToLocation}"
                    }
                }
            };
            order.Nodes.Add(endNode);

            return order;
        }

        /// <summary>
        /// Publishes an AssignmentPlan as a VDA 5050 Order.
        /// Convenience method that builds the order and publishes it in one call.
        /// </summary>
        public async Task<bool> PublishAssignmentPlanAsync(AssignmentPlan plan, int orderUpdateId = 0)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var order = BuildOrderFromAssignmentPlan(plan, orderUpdateId);
            return await PublishOrderAsync(order, plan.AssignedAmr.Id);
        }
    }
}
