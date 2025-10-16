using FmsSimulator.Models;

namespace FmsSimulator.Services;

public sealed class OptimizedMcdmEngine : IFmsServices.IMcdmEngine
{
    private readonly Dictionary<string, double> _weights;
    private readonly LoggingService _logger = LoggingService.Instance;
    private const double Epsilon = 0.0001;

    public OptimizedMcdmEngine(Dictionary<string, double>? weights = null)
    {
        _weights = weights ?? new Dictionary<string, double>
        {
            ["Time"] = 0.5,
            ["Suitability"] = 0.2,
            ["Battery"] = 0.3
        };
    }

    public OperationResult<AssignmentPlan> SelectBestPlan(IEnumerable<AssignmentPlan> plans)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var scoredPlans = plans.AsParallel()
                                 .Select(ScorePlan)
                                 .OrderByDescending(p => p.FinalScore)
                                 .ToList();

            if (!scoredPlans.Any())
                return OperationResult<AssignmentPlan>.Failure("No valid plans to evaluate");

            var bestPlan = scoredPlans.First();
            
            stopwatch.Stop();
            var metrics = new Dictionary<string, object>
            {
                ["scoringTime"] = stopwatch.ElapsedMilliseconds,
                ["plansEvaluated"] = scoredPlans.Count,
                ["bestScore"] = bestPlan.FinalScore
            };

            return OperationResult<AssignmentPlan>.Success(bestPlan, metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError("McdmEngine", "SelectBestPlan", ex);
            return OperationResult<AssignmentPlan>.Failure(ex.Message);
        }
    }

    private AssignmentPlan ScorePlan(AssignmentPlan plan)
    {
        // Time efficiency score with exponential decay
        var predictedTime = Math.Max(plan.PredictedTimeToComplete, Epsilon);
        var timeScore = Math.Exp(-predictedTime / 10.0); // Exponential decay for time penalty
        
        // Resource utilization score with non-linear scaling
        var payloadUtilization = plan.Task.RequiredPayload / plan.AssignedAmr.MaxPayloadKg;
        var suitabilityScore = Math.Clamp(
            Math.Pow(1.0 - payloadUtilization, 2), // Quadratic scaling for better resource matching
            0.0, 1.0);
        
        // Battery optimization with critical threshold
        var batteryThreshold = 0.2; // Critical battery level
        var batteryScore = plan.AssignedAmr.BatteryLevel < batteryThreshold ?
            Math.Pow(plan.AssignedAmr.BatteryLevel / batteryThreshold, 2) : // Quadratic penalty below threshold
            Math.Clamp(plan.AssignedAmr.BatteryLevel, 0.0, 1.0);

        // Dynamic score weighting based on battery status
        var dynamicTimeWeight = plan.AssignedAmr.BatteryLevel < batteryThreshold ? 
            _weights["Time"] * 0.5 : _weights["Time"]; // Reduce time priority for low battery

        plan.FinalScore = (timeScore * dynamicTimeWeight) +
                         (suitabilityScore * _weights["Suitability"]) +
                         (batteryScore * _weights["Battery"]);

        return plan;
    }
}