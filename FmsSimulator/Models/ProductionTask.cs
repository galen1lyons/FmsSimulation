namespace FmsSimulator.Models;

public class ProductionTask
{
    // Basic Properties
    public string TaskId { get; set; } = string.Empty;
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    
    // Requirements for AMR Matching
    public double RequiredPayload { get; set; } = 0;
    public string RequiredModule { get; set; } = string.Empty;
    public double RequiredLiftHeight { get; set; } = 0;
}