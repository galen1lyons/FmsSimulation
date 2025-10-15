using FmsSimulator.Models;

namespace FmsSimulator.Services;

public class SimpleMcdmEngine
{
    // These are our operational priorities. We can change them anytime.
    // Right now, speed is more important than anything else.
    private readonly Dictionary<string, double> _weights = new()
    {
        { "Time", 0.7 },
        { "Suitability", 0.3 } 
    };

    public AssignmentPlan? SelectBestPlan(List<AssignmentPlan> plans)
    {
        Console.WriteLine("--- MCDM Engine ---");
        
        // If there are no valid plans, we can't make a decision.
        if (!plans.Any())
        {
            Console.WriteLine("No plans to evaluate.");
            return null;
        }

        Console.WriteLine("Scoring all valid plans...");
        foreach (var plan in plans)
        {
            // --- Scoring Logic ---

            // 1. Calculate Time Score. For now, we'll just use distance.
            // Lower distance is better, so we invert it for the score.
            double distance = CalculateDistance(plan.AssignedAmr.CurrentPosition, (5,5)); // Fake target for now
            double timeScore = 1.0 / distance;

            // 2. Calculate Suitability Score.
            // A robot with more payload capacity than required is a better "fit".
            double suitabilityScore = 1 - (plan.Task.RequiredPayload / plan.AssignedAmr.MaxPayloadKg);

            // 3. Combine scores using our weights to get the final score.
            plan.FinalScore = (timeScore * _weights["Time"]) + (suitabilityScore * _weights["Suitability"]);
        }

        // Find the plan with the highest score and return it as the winner.
        var bestPlan = plans.OrderByDescending(p => p.FinalScore).First();
        Console.WriteLine($"Best plan found for AMR: {bestPlan.AssignedAmr.Id} with score: {bestPlan.FinalScore:F2}");
        return bestPlan;
    }

    // A helper method to calculate the distance between two points.
    private double CalculateDistance((int X, int Y) start, (int X, int Y) end)
    {
        return Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
    }
}