using FmsSimulator.Models;

namespace FmsSimulator.Services;

public class PlanGenerator
{
    // This method finds all suitable AMRs for a given task.
    public List<AssignmentPlan> GeneratePlans(ProductionTask task, List<AmrState> fleet)
    {
        Console.WriteLine("--- Plan Generator ---");
        Console.WriteLine($"Filtering fleet for task: {task.TaskId}");

        // --- Hard Pruning Logic ---
        // Find AMRs that meet all the task's non-negotiable requirements.
        var suitableAmrs = fleet.Where(amr =>
                amr.IsAvailable &&
                amr.MaxPayloadKg >= task.RequiredPayload &&
                amr.TopModuleType == task.RequiredModule &&
                (task.RequiredModule != "Electric AGV Lift" || amr.LiftingHeightMm >= task.RequiredLiftHeight)
        ).ToList();

        Console.WriteLine($"{suitableAmrs.Count} suitable AMR(s) found.");

        var plans = new List<AssignmentPlan>();
        foreach (var amr in suitableAmrs)
        {
            // For now, we just create a basic plan.
            // We'll add scoring logic later.
            var plan = new AssignmentPlan 
            { 
                AssignedAmr = amr, 
                Task = task 
            };
            plans.Add(plan);
        }

        return plans;
    }
}