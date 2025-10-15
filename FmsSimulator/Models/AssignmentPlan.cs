namespace FmsSimulator.Models;

public class AssignmentPlan
{
    // A plan must have an AMR and a Task
    // We use 'null!' to tell C# "we promise to assign these later".
    public AmrState AssignedAmr { get; set; } = null!;
    public ProductionTask Task { get; set; } = null!;
    
    // Calculated scores for this plan
    public double TimeToComplete { get; set; } = 0;
    public double EnergyConsumed { get; set; } = 0;
    public double FinalScore { get; set; } = 0;
}