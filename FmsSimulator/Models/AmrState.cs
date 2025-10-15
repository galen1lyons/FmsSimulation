namespace FmsSimulator.Models;

public class AmrState
{
    // Basic Properties
    public required string Id { get; set; }
    public double BatteryLevel { get; set; }
    public bool IsAvailable { get; set; }
    public (int X, int Y) CurrentPosition { get; set; }

    // Detailed Spec Properties
    public required string ModelName { get; set; }
    public required string PrimaryMission { get; set; }
    public double MaxPayloadKg { get; set; }
    public double MaxSpeedMS { get; set; }
    public required string TopModuleType { get; set; }

    // Module-specific Capabilities
    public double LiftingHeightMm { get; set; }
    public double ArmReachMm { get; set; }
}
