using FmsSimulator.Models;

namespace FmsSimulator.Services;

public class PlanGenerator
{
    private readonly Dictionary<string, double> _humanTrafficCost = new();
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

    public void UpdateTrafficCost(string zone, double increase)
    {
        if (_humanTrafficCost.ContainsKey(zone))
        {
            _humanTrafficCost[zone] += increase;
            Console.WriteLine($"   [Learning Service]: Traffic cost for {zone} increased to {_humanTrafficCost[zone]:F2}.");
        }
        else
        {
            _humanTrafficCost[zone] = 1.0 + increase;
            Console.WriteLine($"   [Learning Service]: New traffic zone {zone} logged with cost { _humanTrafficCost[zone]:F2}.");
        }
    }
}