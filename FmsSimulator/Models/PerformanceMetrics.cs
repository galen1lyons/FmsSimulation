namespace FmsSimulator.Models;

/// <summary>
/// Represents a single performance observation for Kaizen analysis
/// </summary>
public record PerformanceObservation
{
    public string TaskId { get; init; } = "";
    public string AmrId { get; init; } = "";
    public DateTime Timestamp { get; init; }
    
    // Prediction vs Actual
    public double PredictedTime { get; init; }
    public double ActualTime { get; init; }
    public double PredictionError => ActualTime - PredictedTime;
    public double PredictionAccuracy => 1.0 - Math.Abs(PredictionError / Math.Max(ActualTime, 0.001));
    
    // Path Information
    public string OriginZone { get; init; } = "";
    public string DestinationZone { get; init; } = "";
    public double PathDistance { get; init; }
    
    // Energy Information
    public double PredictedEnergyConsumption { get; init; }
    public double ActualEnergyConsumption { get; init; }
    public double EnergyPredictionError => ActualEnergyConsumption - PredictedEnergyConsumption;
    
    // Environmental Context
    public double FleetUtilization { get; init; } // Percentage of fleet busy
    public TimeOfDay TimeOfDay => Timestamp.Hour switch
    {
        >= 6 and < 12 => TimeOfDay.Morning,
        >= 12 and < 18 => TimeOfDay.Afternoon,
        >= 18 and < 22 => TimeOfDay.Evening,
        _ => TimeOfDay.Night
    };
}

public enum TimeOfDay
{
    Morning,
    Afternoon,
    Evening,
    Night
}

/// <summary>
/// Statistical analysis of performance trends
/// </summary>
public record TrendAnalysis
{
    public string MetricName { get; init; } = "";
    public double Mean { get; init; }
    public double StandardDeviation { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Trend { get; init; } // Positive = improving, Negative = degrading
    public int SampleCount { get; init; }
    public DateTime LastUpdated { get; init; }
}

/// <summary>
/// Zone-specific performance analytics
/// </summary>
public record ZonePerformanceStats
{
    public string ZoneId { get; init; } = "";
    public int TotalTransits { get; init; }
    public double AverageTransitTime { get; init; }
    public double AverageDelay { get; init; } // Actual - Predicted
    public double Reliability { get; init; } // Percentage within 10% of prediction
    public List<TimeOfDayStats> TimeOfDayBreakdown { get; init; } = new();
    public DateTime LastUpdated { get; init; }
}

public record TimeOfDayStats
{
    public TimeOfDay Period { get; init; }
    public double AverageDelay { get; init; }
    public int SampleCount { get; init; }
}

/// <summary>
/// Kaizen improvement recommendation
/// </summary>
public record ImprovementRecommendation
{
    public string Category { get; init; } = ""; // "Pathfinding", "Energy", "Scheduling"
    public string Description { get; init; } = "";
    public double Priority { get; init; } // 0-1, higher = more important
    public double ExpectedImprovement { get; init; } // Expected percentage improvement
    public Dictionary<string, object> Parameters { get; init; } = new();
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// A* pathfinding tuning parameters
/// </summary>
public record PathfindingTuning
{
    public double HeuristicWeight { get; set; } = 1.0; // A* h(n) multiplier
    public Dictionary<string, double> ZoneCosts { get; set; } = new();
    public Dictionary<string, double> TimeOfDayCosts { get; set; } = new()
    {
        [nameof(TimeOfDay.Morning)] = 1.0,
        [nameof(TimeOfDay.Afternoon)] = 1.2,
        [nameof(TimeOfDay.Evening)] = 1.1,
        [nameof(TimeOfDay.Night)] = 0.9
    };
    public double LearningRate { get; set; } = 0.1; // How aggressively to adjust
    public DateTime LastTuned { get; set; }
}

/// <summary>
/// Energy consumption model tuning
/// </summary>
public record EnergyModelTuning
{
    public double BaseConsumptionRate { get; set; } = 1.0;
    public double LoadFactor { get; set; } = 0.5; // Impact of payload on consumption
    public double SpeedFactor { get; set; } = 0.3; // Impact of speed on consumption
    public Dictionary<string, double> AmrSpecificFactors { get; set; } = new();
    public double LearningRate { get; set; } = 0.05;
    public DateTime LastTuned { get; set; }
}
