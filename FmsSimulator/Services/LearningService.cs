using FmsSimulator.Models;
using Microsoft.Extensions.Logging;

namespace FmsSimulator.Services;

/// <summary>
/// Phase 3: Learning Service implementing PDCA (Plan-Do-Check-Act) Kaizen Cycle
/// This represents the "Act" phase where the FMS learns from performance and updates its world model
/// </summary>
public class LearningService : IFmsServices.ILearningService
{
    private readonly ILogger<LearningService> _logger;
    private readonly LoggingService _structuredLogger = LoggingService.Instance;
    private readonly PerformanceAnalyticsService _analytics = new();
    
    // Self-tuning parameters (advanced features)
    private PathfindingTuning _pathfindingTuning = new();
    private EnergyModelTuning _energyTuning = new();
    
    // Learning control parameters
    private const double SignificantErrorThreshold = 2.0; // seconds - matches your specification
    private const double EnergyErrorThreshold = 0.2; // 20% error
    private const int TuningInterval = 10; // Tune every 10 observations
    private int _observationCount = 0;

    public LearningService(ILogger<LearningService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Core PDCA "Act" Phase: Update the world model based on actual vs predicted performance
    /// This is called after each task completes to implement the Kaizen learning loop
    /// </summary>
    public void UpdateWorldModel(AssignmentPlan completedPlan, double actualTimeToComplete, IFmsServices.IPlanGenerator planGenerator)
    {
        _observationCount++;
        
        // === CHECK Phase: Compare prediction with actual result ===
        double error = actualTimeToComplete - completedPlan.PredictedTimeToComplete;
        double relativeError = Math.Abs(error) / Math.Max(actualTimeToComplete, 0.001);

        Console.WriteLine($"\n[Learning Service] Task {completedPlan.Task.TaskId}:");
        Console.WriteLine($"  Predicted: {completedPlan.PredictedTimeToComplete:F2}s");
        Console.WriteLine($"  Actual:    {actualTimeToComplete:F2}s");
        Console.WriteLine($"  Error:     {error:F2}s ({relativeError:P1})");

        // Record observation for analytics (advanced feature)
        var observation = CreateObservation(completedPlan, actualTimeToComplete);
        _analytics.RecordObservation(observation);

        // === ACT Phase: If error is significant, update the internal model ===
        if (Math.Abs(error) > SignificantErrorThreshold)
        {
            Console.WriteLine($"[Learning Service] Task {completedPlan.Task.TaskId} was slower than predicted. Analyzing path...");
            
            // Create zone name based on AMR's starting coordinates
            string zoneToUpdate = $"Zone_{completedPlan.AssignedAmr.CurrentPosition.X}_{completedPlan.AssignedAmr.CurrentPosition.Y}";
            
            // Increase the traffic cost by 10% (0.1) as specified
            planGenerator.UpdateTrafficCost(zoneToUpdate, 0.1);
            
            Console.WriteLine($"[Learning Service] Updated traffic model for {zoneToUpdate}");
        }
        else
        {
            Console.WriteLine($"[Learning Service] Prediction was accurate. No model update needed.");
        }

        // Advanced: Periodic Kaizen continuous improvement (optional enhancement)
        if (_observationCount % TuningInterval == 0)
        {
            PerformAdvancedKaizenTuning(planGenerator);
        }

        // Log metrics
        _structuredLogger.LogOperationalMetrics("LearningService", "WorldModelUpdate", new Dictionary<string, object>
        {
            ["taskId"] = completedPlan.Task.TaskId,
            ["timeError"] = error,
            ["relativeError"] = relativeError,
            ["predictionAccuracy"] = observation.PredictionAccuracy,
            ["systemHealthScore"] = _analytics.GetHealthScore()
        });
    }

    /// <summary>
    /// Immediate reactive adjustments for significant prediction errors
    /// </summary>
    private void ApplyReactiveAdjustments(AssignmentPlan plan, double timeError, IFmsServices.IPlanGenerator planGenerator)
    {
        // Identify the zone that needs adjustment
        string originZone = GetZoneForPosition(plan.AssignedAmr.CurrentPosition);
        
        // Calculate adjustment magnitude based on error severity
        double adjustmentFactor = Math.Clamp(Math.Abs(timeError) / 10.0, 0.05, 0.3);
        double costDelta = timeError > 0 ? adjustmentFactor : -adjustmentFactor * 0.5; // Be more cautious reducing costs

        _logger.LogInformation(
            "[Learning Service] Reactive adjustment: Zone {Zone} cost {Direction} by {Delta:P1}",
            originZone,
            timeError > 0 ? "increased" : "decreased",
            Math.Abs(costDelta)
        );

        planGenerator.UpdateTrafficCost(originZone, costDelta);
        
        // Update pathfinding tuning record
        _pathfindingTuning.ZoneCosts[originZone] = 
            _pathfindingTuning.ZoneCosts.GetValueOrDefault(originZone, 1.0) + costDelta;
    }

    /// <summary>
    /// Advanced Kaizen continuous improvement: Analyze trends and tune models periodically
    /// This is an enhanced feature beyond the basic PDCA loop
    /// </summary>
    private void PerformAdvancedKaizenTuning(IFmsServices.IPlanGenerator planGenerator)
    {
        _logger.LogInformation("[Learning Service] === KAIZEN TUNING CYCLE INITIATED ===");

        // Generate improvement recommendations
        var recommendations = _analytics.GenerateRecommendations();
        
        if (!recommendations.Any())
        {
            _logger.LogInformation("[Learning Service] No significant improvements identified.");
            return;
        }

        foreach (var recommendation in recommendations.Take(3)) // Apply top 3
        {
            _logger.LogInformation(
                "[Learning Service] Recommendation [{Category}] Priority={Priority:F2}: {Description}",
                recommendation.Category,
                recommendation.Priority,
                recommendation.Description
            );

            ApplyRecommendation(recommendation, planGenerator);
        }

        // Update tuning timestamps
        _pathfindingTuning.LastTuned = DateTime.UtcNow;
        _energyTuning.LastTuned = DateTime.UtcNow;

        // Log analytics report
        var report = _analytics.GenerateAnalyticsReport();
        _logger.LogInformation("[Learning Service] Analytics Report:\n{Report}", report);

        _structuredLogger.LogOperationalMetrics("LearningService", "KaizenTuning", new Dictionary<string, object>
        {
            ["recommendationsGenerated"] = recommendations.Count,
            ["systemHealthScore"] = _analytics.GetHealthScore(),
            ["pathfindingTuningAge"] = (DateTime.UtcNow - _pathfindingTuning.LastTuned).TotalSeconds
        });
    }

    /// <summary>
    /// Apply Kaizen improvement recommendation
    /// </summary>
    private void ApplyRecommendation(ImprovementRecommendation recommendation, IFmsServices.IPlanGenerator planGenerator)
    {
        switch (recommendation.Category)
        {
            case "Pathfinding":
                ApplyPathfindingRecommendation(recommendation, planGenerator);
                break;
                
            case "Energy":
                ApplyEnergyModelRecommendation(recommendation);
                break;
                
            case "Scheduling":
                ApplySchedulingRecommendation(recommendation);
                break;
        }
    }

    private void ApplyPathfindingRecommendation(ImprovementRecommendation recommendation, IFmsServices.IPlanGenerator planGenerator)
    {
        if (recommendation.Parameters.TryGetValue("ZoneId", out var zoneIdObj) &&
            recommendation.Parameters.TryGetValue("SuggestedCostIncrease", out var costIncreaseObj))
        {
            var zoneId = zoneIdObj?.ToString() ?? "";
            var costIncrease = Convert.ToDouble(costIncreaseObj);
            
            // Apply graduated adjustment using learning rate
            var actualIncrease = costIncrease * _pathfindingTuning.LearningRate;
            planGenerator.UpdateTrafficCost(zoneId, actualIncrease);
            
            _logger.LogInformation(
                "[Learning Service] Applied pathfinding tuning: Zone {Zone} cost increased by {Increase:F4}",
                zoneId,
                actualIncrease
            );
        }
    }

    private void ApplyEnergyModelRecommendation(ImprovementRecommendation recommendation)
    {
        if (recommendation.Parameters.TryGetValue("AverageError", out var errorObj))
        {
            var avgError = Convert.ToDouble(errorObj);
            
            // Adjust base consumption rate
            var adjustment = avgError * _energyTuning.LearningRate;
            _energyTuning.BaseConsumptionRate += adjustment;
            
            _logger.LogInformation(
                "[Learning Service] Applied energy model tuning: Base consumption rate adjusted to {Rate:F4}",
                _energyTuning.BaseConsumptionRate
            );
        }
    }

    private void ApplySchedulingRecommendation(ImprovementRecommendation recommendation)
    {
        if (recommendation.Parameters.TryGetValue("TimeOfDay", out var periodObj) &&
            recommendation.Parameters.TryGetValue("SuggestedFactor", out var factorObj))
        {
            var period = periodObj?.ToString() ?? "";
            var factor = Convert.ToDouble(factorObj);
            
            _pathfindingTuning.TimeOfDayCosts[period] = factor;
            
            _logger.LogInformation(
                "[Learning Service] Applied scheduling tuning: {Period} factor set to {Factor:F2}",
                period,
                factor
            );
        }
    }

    private PerformanceObservation CreateObservation(AssignmentPlan plan, double actualTime)
    {
        var originZone = GetZoneForPosition(plan.AssignedAmr.CurrentPosition);
        // Use a default destination zone (in production, this would be parsed from ToLocation)
        var destinationZone = $"Zone_5_5"; // Simplified - assuming center target
        
        // Simulate energy consumption (in production, this would come from actual telemetry)
        var predictedEnergy = plan.PredictedTimeToComplete * 0.1 * (1.0 - plan.AssignedAmr.BatteryLevel);
        var actualEnergy = actualTime * 0.1 * (1.0 - plan.AssignedAmr.BatteryLevel) * (0.9 + new Random().NextDouble() * 0.2);

        // Calculate approximate path distance
        var pathDistance = Math.Sqrt(Math.Pow(plan.AssignedAmr.CurrentPosition.X - 5, 2) + Math.Pow(plan.AssignedAmr.CurrentPosition.Y - 5, 2));

        return new PerformanceObservation
        {
            TaskId = plan.Task.TaskId,
            AmrId = plan.AssignedAmr.Id,
            Timestamp = DateTime.UtcNow,
            PredictedTime = plan.PredictedTimeToComplete,
            ActualTime = actualTime,
            OriginZone = originZone,
            DestinationZone = destinationZone,
            PathDistance = pathDistance,
            PredictedEnergyConsumption = predictedEnergy,
            ActualEnergyConsumption = actualEnergy,
            FleetUtilization = 0.6 // Simplified - would be calculated from actual fleet state
        };
    }

    private static string GetZoneForPosition((int X, int Y) pos) =>
        $"Zone_{pos.X / 10}_{pos.Y / 10}";

    private static double CalculateDistance((int X, int Y) start, (int X, int Y) end) =>
        Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));

    /// <summary>
    /// Get current analytics service (for external access)
    /// </summary>
    public PerformanceAnalyticsService GetAnalytics() => _analytics;

    /// <summary>
    /// Get current pathfinding tuning parameters
    /// </summary>
    public PathfindingTuning GetPathfindingTuning() => _pathfindingTuning;

    /// <summary>
    /// Get current energy model tuning parameters
    /// </summary>
    public EnergyModelTuning GetEnergyTuning() => _energyTuning;
}