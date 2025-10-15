namespace FmsSimulator.Models;

public class AssignmentPlan
{
    public AmrState AssignedAmr { get; set; } = null!;
    public ProductionTask Task { get; set; } = null!;
    
    // This is the corrected property name
    public double PredictedTimeToComplete { get; set; } = 0; 
    
    public double EnergyConsumed { get; set; } = 0;
    public double FinalScore { get; set; } = 0;
}