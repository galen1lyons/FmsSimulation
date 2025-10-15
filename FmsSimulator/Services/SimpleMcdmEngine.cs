using FmsSimulator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FmsSimulator.Services;

public class SimpleMcdmEngine : IMcdmEngine
{
    private readonly ILogger<SimpleMcdmEngine> _logger;
    private readonly (int X, int Y) _targetPosition;

    public SimpleMcdmEngine(ILogger<SimpleMcdmEngine> logger, IConfiguration configuration)
    {
        _logger = logger;
        _targetPosition = (configuration.GetValue<int>("FmsSettings:TargetPosition:X"), 
                         configuration.GetValue<int>("FmsSettings:TargetPosition:Y"));
    }

    // Backwards-compatible parameterless constructor for tests and direct instantiation
    public SimpleMcdmEngine() : this(NullLogger<SimpleMcdmEngine>.Instance, new ConfigurationBuilder().Build())
    {
    }
    // Quick-win constants
    private const double Epsilon = 0.001;

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
        _logger.LogInformation("--- MCDM Engine (Smart) ---");

        if (!plans.Any())
        {
            _logger.LogWarning("No plans to evaluate.");
            return null;
        }

        _logger.LogInformation("Scoring all valid plans using Time, Suitability, and Battery...");
        foreach (var plan in plans)
        {
            // --- Scoring Logic ---

            // 1. Calculate Time Score (same as before).
            plan.PredictedTimeToComplete = CalculateDistance(plan.AssignedAmr.CurrentPosition, _targetPosition);
            // Guard: avoid divide-by-zero if distance is 0.
            if (plan.PredictedTimeToComplete <= 0.0)
            {
                plan.PredictedTimeToComplete = Epsilon;
            }
            // Normalize time score to a 0..1 range using a simple heuristic (1 / (1 + distance)).
            double timeScore = 1.0 / (1.0 + plan.PredictedTimeToComplete);

            // 2. Calculate Suitability Score (same as before).
            double suitabilityScore = 1.0 - (plan.Task.RequiredPayload / plan.AssignedAmr.MaxPayloadKg);
            suitabilityScore = Math.Clamp(suitabilityScore, 0.0, 1.0);

            // 3. NEW: Calculate Battery Score.
            // The battery level is already a 0.0 to 1.0 value, which is perfect for a score.
            double batteryScore = Math.Clamp(plan.AssignedAmr.BatteryLevel, 0.0, 1.0);

            // 4. NEW: Update the final score calculation to include the new criterion.
            // Final score normalized to expected ranges.
            plan.FinalScore = (timeScore * _weights["Time"]) +
                              (suitabilityScore * _weights["Suitability"]) +
                              (batteryScore * _weights["Battery"]);
            plan.FinalScore = Math.Clamp(plan.FinalScore, 0.0, 1.0);
        }

        var bestPlan = plans.OrderByDescending(p => p.FinalScore).First();
        _logger.LogInformation("Best plan found for AMR: {AmrId} with score: {Score:F2}", bestPlan.AssignedAmr.Id, bestPlan.FinalScore);
        return bestPlan;
    }

    private double CalculateDistance((int X, int Y) start, (int X, int Y) end)
    {
        return Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
    }
}