namespace FmsSimulator.Models;

// Represents a message sent on the AMR's internal MQTT bus.
public class InternalAmrMessage
{
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}