using FmsSimulator.Models;

namespace FmsSimulator.Services;

public class SimpleMcdmEngine
{
    // NEW: We've adjusted the weights to include a new "Battery" criterion.
    // Time is still important, but now battery health is a major factor.
    private readonly Dictionary<string, double> _weights = new()
    {
        { "Time", 0.5 },
        { "Suitability", 0.2 },
        { "Battery", 0.3 } 
    };

    public AssignmentPlan? SelectBestPlan(List<AssignmentPlan> plans)
    {
        Console.WriteLine("--- MCDM Engine (Smart) ---");
        
        if (!plans.Any())
        {
            Console.WriteLine("No plans to evaluate.");
            return null;
        }

        Console.WriteLine("Scoring all valid plans using Time, Suitability, and Battery...");
        foreach (var plan in plans)
        {
            // --- Scoring Logic ---

            // 1. Calculate Time Score (same as before).
            plan.PredictedTimeToComplete = CalculateDistance(plan.AssignedAmr.CurrentPosition, (5,5));
            double timeScore = 1.0 / plan.PredictedTimeToComplete;

            // 2. Calculate Suitability Score (same as before).
            double suitabilityScore = 1 - (plan.Task.RequiredPayload / plan.AssignedAmr.MaxPayloadKg);

            // 3. NEW: Calculate Battery Score.
            // The battery level is already a 0.0 to 1.0 value, which is perfect for a score.
            double batteryScore = plan.AssignedAmr.BatteryLevel;

            // 4. NEW: Update the final score calculation to include the new criterion.
            plan.FinalScore = (timeScore * _weights["Time"]) +
                              (suitabilityScore * _weights["Suitability"]) +
                              (batteryScore * _weights["Battery"]);
        }

        var bestPlan = plans.OrderByDescending(p => p.FinalScore).First();
        Console.WriteLine($"Best plan found for AMR: {bestPlan.AssignedAmr.Id} with score: {bestPlan.FinalScore:F2}");
        return bestPlan;
    }

    private double CalculateDistance((int X, int Y) start, (int X, int Y) end)
    {
        return Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
    }
}