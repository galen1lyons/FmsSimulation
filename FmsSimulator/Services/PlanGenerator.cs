using FmsSimulator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FmsSimulator.Services;

public class PlanGenerator : IPlanGenerator
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, double> _humanTrafficCost = new();
    private readonly string _trafficFilePath;
    private readonly ILogger<PlanGenerator> _logger;

    public PlanGenerator(ILogger<PlanGenerator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _trafficFilePath = configuration["FmsSettings:TrafficDataPath"] ?? "data/traffic.json";
        var dataDir = Path.GetDirectoryName(_trafficFilePath);
        if (!string.IsNullOrEmpty(dataDir))         {
            Directory.CreateDirectory(dataDir);
        }
        LoadTrafficCosts();
    }

    // This method finds all suitable AMRs for a given task.
    public List<AssignmentPlan> GeneratePlans(ProductionTask task, List<AmrState> fleet)
    {
        _logger.LogInformation("--- Plan Generator ---");
        _logger.LogInformation("Filtering fleet for task: {TaskId}", task.TaskId);

        // --- Hard Pruning Logic ---
        // Find AMRs that meet all the task's non-negotiable requirements.
        var suitableAmrs = fleet.Where(amr =>
                amr.IsAvailable &&
                amr.MaxPayloadKg >= task.RequiredPayload &&
                amr.TopModuleType == task.RequiredModule &&
                (task.RequiredLiftHeight == 0 || amr.LiftingHeightMm >= task.RequiredLiftHeight)
        ).ToList();

        _logger.LogInformation("{Count} suitable AMR(s) found.", suitableAmrs.Count);

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
        _humanTrafficCost.AddOrUpdate(zone, 1.0 + increase, (k, v) => v + increase);
        _logger.LogInformation("[Learning Service]: Traffic cost for {Zone} is now {Cost:F2}.", zone, _humanTrafficCost[zone]);
        SaveTrafficCosts();
    }

    private void LoadTrafficCosts()
    {
        try
        {
            if (!File.Exists(_trafficFilePath)) return;
            var json = File.ReadAllText(_trafficFilePath);
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(json);
            if (dict == null) return;
            foreach (var kv in dict)
            {
                _humanTrafficCost[kv.Key] = kv.Value;
            }
            _logger.LogInformation("[PlanGenerator]: Loaded {Count} traffic entries from disk.", _humanTrafficCost.Count);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "[PlanGenerator]: IO error while loading traffic costs: {Message}", ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "[PlanGenerator]: JSON parsing error while loading traffic costs: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlanGenerator]: Unexpected error while loading traffic costs: {Message}", ex.Message);
        }
    }

    private void SaveTrafficCosts()
    {
        try
        {
            var dict = _humanTrafficCost.ToDictionary(kv => kv.Key, kv => kv.Value);
            var json = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_trafficFilePath, json);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "[PlanGenerator]: IO error while saving traffic costs: {Message}", ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "[PlanGenerator]: JSON parsing error while saving traffic costs: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlanGenerator]: Unexpected error while saving traffic costs: {Message}", ex.Message);
        }
    }
}