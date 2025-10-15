namespace FmsSimulator.Models;

// RENAMED: Represents a command sent on the AMR's internal MQTT bus.
public class InternalAmrCommand
{
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}