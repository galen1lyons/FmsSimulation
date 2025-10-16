using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FmsSimulator.Services.MQTT
{
    // ==================================================================================
    // VDA 5050 V2.0 PROTOCOL MODELS
    // ==================================================================================
    // Complete implementation of VDA 5050 standard for AGV communication.
    // Reference: https://github.com/VDA5050/VDA5050

    // ==================================================================================
    // COMMON TYPES
    // ==================================================================================

    /// <summary>
    /// Header information included in all VDA 5050 messages.
    /// </summary>
    public class HeaderMessage
    {
        [JsonPropertyName("headerId")]
        public int HeaderId { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Node position in the map coordinate system.
    /// </summary>
    public class NodePosition
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("theta")]
        public double? Theta { get; set; }

        [JsonPropertyName("allowedDeviationXy")]
        public double? AllowedDeviationXy { get; set; }

        [JsonPropertyName("allowedDeviationTheta")]
        public double? AllowedDeviationTheta { get; set; }

        [JsonPropertyName("mapId")]
        public string MapId { get; set; } = string.Empty;

        [JsonPropertyName("mapDescription")]
        public string? MapDescription { get; set; }
    }

    /// <summary>
    /// Action to be executed by the AGV.
    /// </summary>
    public class Action
    {
        [JsonPropertyName("actionType")]
        public string ActionType { get; set; } = string.Empty;

        [JsonPropertyName("actionId")]
        public string ActionId { get; set; } = string.Empty;

        [JsonPropertyName("actionDescription")]
        public string? ActionDescription { get; set; }

        [JsonPropertyName("blockingType")]
        public BlockingType BlockingType { get; set; } = BlockingType.NONE;

        [JsonPropertyName("actionParameters")]
        public List<ActionParameter>? ActionParameters { get; set; }
    }

    /// <summary>
    /// Parameter for an action.
    /// </summary>
    public class ActionParameter
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Blocking behavior of an action.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BlockingType
    {
        NONE,
        SOFT,
        HARD
    }

    // ==================================================================================
    // ORDER MESSAGE (FMS → AGV)
    // ==================================================================================

    /// <summary>
    /// Order message sent from FMS to AGV. Contains the complete mission with nodes and edges.
    /// Topic: vda5050/v2/{manufacturer}/{agvId}/order
    /// </summary>
    public class OrderMessage
    {
        [JsonPropertyName("headerId")]
        public int HeaderId { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("orderUpdateId")]
        public int OrderUpdateId { get; set; }

        [JsonPropertyName("zoneSetId")]
        public string? ZoneSetId { get; set; }

        [JsonPropertyName("nodes")]
        public List<Node> Nodes { get; set; } = new();

        [JsonPropertyName("edges")]
        public List<Edge> Edges { get; set; } = new();
    }

    /// <summary>
    /// Node (waypoint) in the order.
    /// </summary>
    public class Node
    {
        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("sequenceId")]
        public int SequenceId { get; set; }

        [JsonPropertyName("released")]
        public bool Released { get; set; } = true;

        [JsonPropertyName("nodeDescription")]
        public string? NodeDescription { get; set; }

        [JsonPropertyName("nodePosition")]
        public NodePosition? NodePosition { get; set; }

        [JsonPropertyName("actions")]
        public List<Action>? Actions { get; set; }
    }

    /// <summary>
    /// Edge (path segment) in the order connecting two nodes.
    /// </summary>
    public class Edge
    {
        [JsonPropertyName("edgeId")]
        public string EdgeId { get; set; } = string.Empty;

        [JsonPropertyName("sequenceId")]
        public int SequenceId { get; set; }

        [JsonPropertyName("released")]
        public bool Released { get; set; } = true;

        [JsonPropertyName("edgeDescription")]
        public string? EdgeDescription { get; set; }

        [JsonPropertyName("startNodeId")]
        public string StartNodeId { get; set; } = string.Empty;

        [JsonPropertyName("endNodeId")]
        public string EndNodeId { get; set; } = string.Empty;

        [JsonPropertyName("maxSpeed")]
        public double? MaxSpeed { get; set; }

        [JsonPropertyName("maxHeight")]
        public double? MaxHeight { get; set; }

        [JsonPropertyName("minHeight")]
        public double? MinHeight { get; set; }

        [JsonPropertyName("orientation")]
        public double? Orientation { get; set; }

        [JsonPropertyName("orientationType")]
        public OrientationType? OrientationType { get; set; }

        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        [JsonPropertyName("rotationAllowed")]
        public bool? RotationAllowed { get; set; }

        [JsonPropertyName("maxRotationSpeed")]
        public double? MaxRotationSpeed { get; set; }

        [JsonPropertyName("trajectory")]
        public Trajectory? Trajectory { get; set; }

        [JsonPropertyName("length")]
        public double? Length { get; set; }

        [JsonPropertyName("actions")]
        public List<Action>? Actions { get; set; }
    }

    /// <summary>
    /// Orientation type for edge traversal.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrientationType
    {
        GLOBAL,
        TANGENTIAL
    }

    /// <summary>
    /// Trajectory definition for an edge (spline, Bezier curve, etc.).
    /// </summary>
    public class Trajectory
    {
        [JsonPropertyName("degree")]
        public int Degree { get; set; }

        [JsonPropertyName("knotVector")]
        public List<double> KnotVector { get; set; } = new();

        [JsonPropertyName("controlPoints")]
        public List<ControlPoint> ControlPoints { get; set; } = new();
    }

    /// <summary>
    /// Control point for trajectory definition.
    /// </summary>
    public class ControlPoint
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("weight")]
        public double? Weight { get; set; }
    }

    // ==================================================================================
    // INSTANT ACTIONS MESSAGE (FMS → AGV)
    // ==================================================================================

    /// <summary>
    /// Instant actions message for immediate execution (e.g., emergency stop, pause).
    /// Topic: vda5050/v2/{manufacturer}/{agvId}/instantActions
    /// </summary>
    public class InstantActionsMessage
    {
        [JsonPropertyName("headerId")]
        public int HeaderId { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("instantActions")]
        public List<Action> InstantActions { get; set; } = new();
    }

    // ==================================================================================
    // STATE MESSAGE (AGV → FMS)
    // ==================================================================================

    /// <summary>
    /// State message sent from AGV to FMS. Contains current status, position, and action states.
    /// Topic: vda5050/v2/{manufacturer}/{agvId}/state
    /// </summary>
    public class StateMessage
    {
        [JsonPropertyName("headerId")]
        public int HeaderId { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("orderUpdateId")]
        public int OrderUpdateId { get; set; }

        [JsonPropertyName("zoneSetId")]
        public string? ZoneSetId { get; set; }

        [JsonPropertyName("lastNodeId")]
        public string LastNodeId { get; set; } = string.Empty;

        [JsonPropertyName("lastNodeSequenceId")]
        public int LastNodeSequenceId { get; set; }

        [JsonPropertyName("driving")]
        public bool Driving { get; set; }

        [JsonPropertyName("paused")]
        public bool? Paused { get; set; }

        [JsonPropertyName("newBaseRequest")]
        public bool? NewBaseRequest { get; set; }

        [JsonPropertyName("distanceSinceLastNode")]
        public double? DistanceSinceLastNode { get; set; }

        [JsonPropertyName("operatingMode")]
        public OperatingMode OperatingMode { get; set; } = OperatingMode.AUTOMATIC;

        [JsonPropertyName("nodeStates")]
        public List<NodeState>? NodeStates { get; set; }

        [JsonPropertyName("edgeStates")]
        public List<EdgeState>? EdgeStates { get; set; }

        [JsonPropertyName("actionStates")]
        public List<ActionState>? ActionStates { get; set; }

        [JsonPropertyName("agvPosition")]
        public AgvPosition? AgvPosition { get; set; }

        [JsonPropertyName("velocity")]
        public Velocity? Velocity { get; set; }

        [JsonPropertyName("loads")]
        public List<Load>? Loads { get; set; }

        [JsonPropertyName("batteryState")]
        public BatteryState? BatteryState { get; set; }

        [JsonPropertyName("errors")]
        public List<Error>? Errors { get; set; }

        [JsonPropertyName("information")]
        public List<Information>? Information { get; set; }

        [JsonPropertyName("safetyState")]
        public SafetyState? SafetyState { get; set; }
    }

    /// <summary>
    /// Operating mode of the AGV.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OperatingMode
    {
        AUTOMATIC,
        SEMIAUTOMATIC,
        MANUAL,
        SERVICE,
        TEACHIN
    }

    /// <summary>
    /// State of a node in the current order.
    /// </summary>
    public class NodeState
    {
        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("sequenceId")]
        public int SequenceId { get; set; }

        [JsonPropertyName("released")]
        public bool Released { get; set; }

        [JsonPropertyName("nodeDescription")]
        public string? NodeDescription { get; set; }

        [JsonPropertyName("nodePosition")]
        public NodePosition? NodePosition { get; set; }
    }

    /// <summary>
    /// State of an edge in the current order.
    /// </summary>
    public class EdgeState
    {
        [JsonPropertyName("edgeId")]
        public string EdgeId { get; set; } = string.Empty;

        [JsonPropertyName("sequenceId")]
        public int SequenceId { get; set; }

        [JsonPropertyName("released")]
        public bool Released { get; set; }

        [JsonPropertyName("edgeDescription")]
        public string? EdgeDescription { get; set; }

        [JsonPropertyName("trajectory")]
        public Trajectory? Trajectory { get; set; }
    }

    /// <summary>
    /// State of an action being executed.
    /// </summary>
    public class ActionState
    {
        [JsonPropertyName("actionId")]
        public string ActionId { get; set; } = string.Empty;

        [JsonPropertyName("actionType")]
        public string ActionType { get; set; } = string.Empty;

        [JsonPropertyName("actionDescription")]
        public string? ActionDescription { get; set; }

        [JsonPropertyName("actionStatus")]
        public ActionStatus ActionStatus { get; set; } = ActionStatus.WAITING;

        [JsonPropertyName("resultDescription")]
        public string? ResultDescription { get; set; }
    }

    /// <summary>
    /// Status of an action.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ActionStatus
    {
        WAITING,
        INITIALIZING,
        RUNNING,
        PAUSED,
        FINISHED,
        FAILED
    }

    /// <summary>
    /// Current position and orientation of the AGV.
    /// </summary>
    public class AgvPosition
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("theta")]
        public double Theta { get; set; }

        [JsonPropertyName("mapId")]
        public string MapId { get; set; } = string.Empty;

        [JsonPropertyName("mapDescription")]
        public string? MapDescription { get; set; }

        [JsonPropertyName("positionInitialized")]
        public bool PositionInitialized { get; set; } = true;

        [JsonPropertyName("localizationScore")]
        public double? LocalizationScore { get; set; }

        [JsonPropertyName("deviationRange")]
        public double? DeviationRange { get; set; }
    }

    /// <summary>
    /// Current velocity of the AGV.
    /// </summary>
    public class Velocity
    {
        [JsonPropertyName("vx")]
        public double? Vx { get; set; }

        [JsonPropertyName("vy")]
        public double? Vy { get; set; }

        [JsonPropertyName("omega")]
        public double? Omega { get; set; }
    }

    /// <summary>
    /// Load currently carried by the AGV.
    /// </summary>
    public class Load
    {
        [JsonPropertyName("loadId")]
        public string? LoadId { get; set; }

        [JsonPropertyName("loadType")]
        public string? LoadType { get; set; }

        [JsonPropertyName("loadPosition")]
        public string? LoadPosition { get; set; }

        [JsonPropertyName("boundingBoxReference")]
        public BoundingBoxReference? BoundingBoxReference { get; set; }

        [JsonPropertyName("loadDimensions")]
        public LoadDimensions? LoadDimensions { get; set; }

        [JsonPropertyName("weight")]
        public double? Weight { get; set; }
    }

    /// <summary>
    /// Reference point for bounding box.
    /// </summary>
    public class BoundingBoxReference
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("z")]
        public double Z { get; set; }

        [JsonPropertyName("theta")]
        public double? Theta { get; set; }
    }

    /// <summary>
    /// Dimensions of the load.
    /// </summary>
    public class LoadDimensions
    {
        [JsonPropertyName("length")]
        public double Length { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double? Height { get; set; }
    }

    /// <summary>
    /// Battery state of the AGV.
    /// </summary>
    public class BatteryState
    {
        [JsonPropertyName("batteryCharge")]
        public double BatteryCharge { get; set; }

        [JsonPropertyName("batteryVoltage")]
        public double? BatteryVoltage { get; set; }

        [JsonPropertyName("batteryHealth")]
        public double? BatteryHealth { get; set; }

        [JsonPropertyName("charging")]
        public bool Charging { get; set; }

        [JsonPropertyName("reach")]
        public int? Reach { get; set; }
    }

    /// <summary>
    /// Error reported by the AGV.
    /// </summary>
    public class Error
    {
        [JsonPropertyName("errorType")]
        public string ErrorType { get; set; } = string.Empty;

        [JsonPropertyName("errorReferences")]
        public List<ErrorReference>? ErrorReferences { get; set; }

        [JsonPropertyName("errorDescription")]
        public string? ErrorDescription { get; set; }

        [JsonPropertyName("errorLevel")]
        public ErrorLevel ErrorLevel { get; set; } = ErrorLevel.WARNING;

        [JsonPropertyName("errorHint")]
        public string? ErrorHint { get; set; }
    }

    /// <summary>
    /// Reference to the source of an error.
    /// </summary>
    public class ErrorReference
    {
        [JsonPropertyName("referenceKey")]
        public string ReferenceKey { get; set; } = string.Empty;

        [JsonPropertyName("referenceValue")]
        public string ReferenceValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Severity level of an error.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ErrorLevel
    {
        WARNING,
        FATAL
    }

    /// <summary>
    /// Informational message from the AGV.
    /// </summary>
    public class Information
    {
        [JsonPropertyName("infoType")]
        public string InfoType { get; set; } = string.Empty;

        [JsonPropertyName("infoReferences")]
        public List<InfoReference>? InfoReferences { get; set; }

        [JsonPropertyName("infoDescription")]
        public string? InfoDescription { get; set; }

        [JsonPropertyName("infoLevel")]
        public InfoLevel InfoLevel { get; set; } = InfoLevel.INFO;
    }

    /// <summary>
    /// Reference to the source of information.
    /// </summary>
    public class InfoReference
    {
        [JsonPropertyName("referenceKey")]
        public string ReferenceKey { get; set; } = string.Empty;

        [JsonPropertyName("referenceValue")]
        public string ReferenceValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Information level.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InfoLevel
    {
        DEBUG,
        INFO
    }

    /// <summary>
    /// Safety state of the AGV.
    /// </summary>
    public class SafetyState
    {
        [JsonPropertyName("eStop")]
        public EStopState EStop { get; set; } = EStopState.NONE;

        [JsonPropertyName("fieldViolation")]
        public bool FieldViolation { get; set; }
    }

    /// <summary>
    /// E-Stop state.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EStopState
    {
        NONE,
        AUTOACK,
        MANUAL
    }

    // ==================================================================================
    // VISUALIZATION MESSAGE (AGV → FMS)
    // ==================================================================================

    /// <summary>
    /// Visualization message for rendering AGV state in monitoring tools.
    /// Topic: vda5050/v2/{manufacturer}/{agvId}/visualization
    /// </summary>
    public class VisualizationMessage
    {
        [JsonPropertyName("headerId")]
        public int HeaderId { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("agvPosition")]
        public AgvPosition? AgvPosition { get; set; }

        [JsonPropertyName("velocity")]
        public Velocity? Velocity { get; set; }
    }

    // ==================================================================================
    // CONNECTION MESSAGE (AGV → FMS)
    // ==================================================================================

    /// <summary>
    /// Connection state message (MQTT Last Will and Testament).
    /// Topic: vda5050/v2/{manufacturer}/{agvId}/connection
    /// </summary>
    public class ConnectionMessage
    {
        [JsonPropertyName("headerId")]
        public int HeaderId { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("connectionState")]
        public ConnectionState ConnectionState { get; set; } = ConnectionState.ONLINE;
    }

    /// <summary>
    /// Connection state enum.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionState
    {
        ONLINE,
        OFFLINE,
        CONNECTIONBROKEN
    }
}
