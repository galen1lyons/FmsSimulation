using FmsSimulator.Models;

namespace FmsSimulator.Services;

public sealed class OptimizedPlanGenerator : IFmsServices.IPlanGenerator
{
    private readonly Dictionary<string, double> _zoneScores = new();
    private readonly LoggingService _logger = LoggingService.Instance;

    public OperationResult<List<AssignmentPlan>> GeneratePlans(ProductionTask task, IEnumerable<AmrState> fleet)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Phase 1: Quick pre-filtering with indexed lookup
            var preFilteredAmrs = fleet
                .Where(amr => amr.IsAvailable && amr.BatteryLevel > 0.1) // Basic availability check
                .AsParallel() // Parallel processing for large fleets
                .ToList();
            
            // Phase 2: Detailed capability matching with early termination
            var suitableAmrs = preFilteredAmrs
                .Where(amr => IsSuitableForTask(amr, task))
                .Take(10) // Limit candidates for large fleets
                .ToList();
            
            if (!suitableAmrs.Any())
                return OperationResult<List<AssignmentPlan>>.Failure("No suitable AMRs found");

            // Phase 3: Generate optimized plans with spatial considerations
            var plans = suitableAmrs
                .AsParallel()
                .Select(amr => CreatePlan(amr, task))
                .OrderByDescending(p => p.PredictedTimeToComplete) // Pre-sort by estimated efficiency
                .Take(5) // Keep top 5 most promising plans
                .ToList();
            
            stopwatch.Stop();
            var metrics = new Dictionary<string, object>
            {
                ["planGenerationTime"] = stopwatch.ElapsedMilliseconds,
                ["suitableAmrsFound"] = suitableAmrs.Count
            };

            return OperationResult<List<AssignmentPlan>>.Success(plans, metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError("PlanGenerator", "GeneratePlans", ex);
            return OperationResult<List<AssignmentPlan>>.Failure(ex.Message);
        }
    }

    private static bool IsSuitableForTask(AmrState amr, ProductionTask task) =>
        amr.IsAvailable &&
        amr.MaxPayloadKg >= task.RequiredPayload &&
        amr.TopModuleType == task.RequiredModule &&
        (task.RequiredModule != "Electric AGV Lift" || amr.LiftingHeightMm >= task.RequiredLiftHeight);

    private AssignmentPlan CreatePlan(AmrState amr, ProductionTask task)
    {
        // Enhanced zone-based scoring with temporal and spatial factors
        var currentZone = GetZoneForPosition(amr.CurrentPosition);
        var targetZone = GetZoneForPosition((5, 5)); // Assuming target is center point
        
        // Get zone scores with temporal decay
        var currentTime = DateTime.UtcNow.TimeOfDay.TotalHours;
        var timeWeight = Math.Sin(currentTime * Math.PI / 12) * 0.2 + 0.8; // Time-of-day factor
        
        var currentZoneScore = _zoneScores.GetValueOrDefault(currentZone, 1.0) * timeWeight;
        var targetZoneScore = _zoneScores.GetValueOrDefault(targetZone, 1.0) * timeWeight;
        
        // Calculate weighted zone score considering both source and destination
        var zoneScore = (currentZoneScore + targetZoneScore) / 2.0;
        
        // Enhanced time prediction with non-linear scaling
        var baseTime = CalculateTime(amr.CurrentPosition, task, zoneScore);
        var adjustedTime = baseTime * (1.0 + (1.0 - amr.BatteryLevel) * 0.5); // Battery impact

        return new AssignmentPlan
        {
            AssignedAmr = amr,
            Task = task,
            PredictedTimeToComplete = adjustedTime
        };
    }

    private static string GetZoneForPosition((int X, int Y) pos) =>
        $"Zone_{pos.X / 10}_{pos.Y / 10}";

    private static double CalculateTime((int X, int Y) start, ProductionTask task, double zoneScore) =>
        Math.Sqrt(Math.Pow(start.X - 5, 2) + Math.Pow(start.Y - 5, 2)) * zoneScore;

    public void UpdateZoneScore(string zone, double delta)
    {
        _zoneScores[zone] = Math.Max(0.1, _zoneScores.GetValueOrDefault(zone, 1.0) + delta);
        _logger.LogPerformanceMetric("PlanGenerator", $"ZoneScore_{zone}", _zoneScores[zone]);
    }

    public void UpdateTrafficCost(string zone, double increase)
    {
        UpdateZoneScore(zone, increase);
    }
}