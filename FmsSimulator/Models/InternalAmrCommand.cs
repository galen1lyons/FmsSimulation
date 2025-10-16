namespace FmsSimulator.Models;

/// <summary>
/// Represents a command sent on the AMR's internal MQTT bus with proper QoS and acknowledgment.
/// </summary>
public class InternalAmrCommand
{
    public required string Topic { get; set; }
    public required string Payload { get; set; }
    public required QosLevel QosLevel { get; set; }
    public bool RetainMessage { get; set; }
    public required string CorrelationId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a response from an AMR's internal subsystem to a command.
/// </summary>
public class InternalAmrResponse
{
    public required string CorrelationId { get; set; }
    public required string Topic { get; set; }
    public required string Payload { get; set; }
    public required ResponseStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// MQTT Quality of Service levels.
/// </summary>
public enum QosLevel
{
    AtMostOnce = 0,
    AtLeastOnce = 1,
    ExactlyOnce = 2
}

/// <summary>
/// Status codes for internal AMR command responses.
/// </summary>
public enum ResponseStatus
{
    Success,
    InProgress,
    Failed,
    Rejected
}