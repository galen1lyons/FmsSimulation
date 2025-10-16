using FmsSimulator.Models;
using System.Collections.Concurrent;

namespace FmsSimulator.Services;

/// <summary>
/// Phase 3: Performance Analytics Service for Kaizen feedback loop
/// Collects, analyzes, and provides insights for continuous improvement
/// </summary>
public sealed class PerformanceAnalyticsService
{
    private readonly ConcurrentQueue<PerformanceObservation> _observations = new();
    private readonly Dictionary<string, ZonePerformanceStats> _zoneStats = new();
    private readonly LoggingService _logger = LoggingService.Instance;
    private readonly object _lock = new();
    
    private const int MaxObservations = 10000; // Rolling window
    private const int MinSamplesForAnalysis = 10;

    /// <summary>
    /// Record a completed task for analysis
    /// </summary>
    public void RecordObservation(PerformanceObservation observation)
    {
        _observations.Enqueue(observation);
        
        // Maintain rolling window
        while (_observations.Count > MaxObservations)
        {
            _observations.TryDequeue(out _);
        }
        
        UpdateZoneStatistics(observation);
        
        _logger.LogPerformanceMetric("Analytics", "ObservationRecorded", observation.PredictionAccuracy);
    }

    /// <summary>
    /// Calculate trend analysis for a specific metric
    /// </summary>
    public TrendAnalysis AnalyzeTrend(Func<PerformanceObservation, double> metricSelector, string metricName)
    {
        var observations = _observations.ToArray();
        
        if (observations.Length < MinSamplesForAnalysis)
        {
            return new TrendAnalysis
            {
                MetricName = metricName,
                SampleCount = observations.Length,
                LastUpdated = DateTime.UtcNow
            };
        }

        var values = observations.Select(metricSelector).ToArray();
        var mean = values.Average();
        var stdDev = Math.Sqrt(values.Select(v => Math.Pow(v - mean, 2)).Average());
        
        // Calculate trend using linear regression over time
        var trend = CalculateTrend(observations, metricSelector);

        return new TrendAnalysis
        {
            MetricName = metricName,
            Mean = mean,
            StandardDeviation = stdDev,
            Min = values.Min(),
            Max = values.Max(),
            Trend = trend,
            SampleCount = observations.Length,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Get performance statistics for a specific zone
    /// </summary>
    public ZonePerformanceStats? GetZoneStatistics(string zoneId)
    {
        lock (_lock)
        {
            return _zoneStats.GetValueOrDefault(zoneId);
        }
    }

    /// <summary>
    /// Get all zone statistics
    /// </summary>
    public IReadOnlyDictionary<string, ZonePerformanceStats> GetAllZoneStatistics()
    {
        lock (_lock)
        {
            return new Dictionary<string, ZonePerformanceStats>(_zoneStats);
        }
    }

    /// <summary>
    /// Generate Kaizen improvement recommendations
    /// </summary>
    public List<ImprovementRecommendation> GenerateRecommendations()
    {
        var recommendations = new List<ImprovementRecommendation>();
        var observations = _observations.ToArray();

        if (observations.Length < MinSamplesForAnalysis)
            return recommendations;

        // Analyze prediction accuracy
        var accuracyTrend = AnalyzeTrend(o => o.PredictionAccuracy, "PredictionAccuracy");
        if (accuracyTrend.Mean < 0.8) // Less than 80% accuracy
        {
            recommendations.Add(new ImprovementRecommendation
            {
                Category = "Pathfinding",
                Description = $"Overall prediction accuracy is {accuracyTrend.Mean:P0}. Consider tuning pathfinding heuristics.",
                Priority = 0.9,
                ExpectedImprovement = (0.8 - accuracyTrend.Mean) * 100,
                Parameters = new Dictionary<string, object>
                {
                    ["CurrentAccuracy"] = accuracyTrend.Mean,
                    ["TargetAccuracy"] = 0.8
                },
                GeneratedAt = DateTime.UtcNow
            });
        }

        // Identify problematic zones
        var problematicZones = _zoneStats
            .Where(z => z.Value.Reliability < 0.7 && z.Value.TotalTransits > 5)
            .OrderBy(z => z.Value.Reliability)
            .Take(3);

        foreach (var zone in problematicZones)
        {
            recommendations.Add(new ImprovementRecommendation
            {
                Category = "Pathfinding",
                Description = $"Zone {zone.Key} has low reliability ({zone.Value.Reliability:P0}) with average delay of {zone.Value.AverageDelay:F2}s",
                Priority = 0.7,
                ExpectedImprovement = (0.9 - zone.Value.Reliability) * 100,
                Parameters = new Dictionary<string, object>
                {
                    ["ZoneId"] = zone.Key,
                    ["CurrentReliability"] = zone.Value.Reliability,
                    ["AverageDelay"] = zone.Value.AverageDelay,
                    ["SuggestedCostIncrease"] = zone.Value.AverageDelay * 0.1
                },
                GeneratedAt = DateTime.UtcNow
            });
        }

        // Analyze energy prediction errors
        var energyErrorTrend = AnalyzeTrend(o => Math.Abs(o.EnergyPredictionError), "EnergyPredictionError");
        if (energyErrorTrend.Mean > 0.2) // More than 20% error
        {
            recommendations.Add(new ImprovementRecommendation
            {
                Category = "Energy",
                Description = $"Energy consumption predictions have high error ({energyErrorTrend.Mean:P0}). Recalibrate energy model.",
                Priority = 0.6,
                ExpectedImprovement = energyErrorTrend.Mean * 50,
                Parameters = new Dictionary<string, object>
                {
                    ["AverageError"] = energyErrorTrend.Mean,
                    ["StandardDeviation"] = energyErrorTrend.StandardDeviation
                },
                GeneratedAt = DateTime.UtcNow
            });
        }

        // Time-of-day analysis
        var timeOfDayPerformance = observations
            .GroupBy(o => o.TimeOfDay)
            .Select(g => new
            {
                Period = g.Key,
                AvgError = g.Average(o => Math.Abs(o.PredictionError)),
                Count = g.Count()
            })
            .Where(x => x.Count > 5)
            .OrderByDescending(x => x.AvgError)
            .FirstOrDefault();

        if (timeOfDayPerformance != null && timeOfDayPerformance.AvgError > 2.0)
        {
            recommendations.Add(new ImprovementRecommendation
            {
                Category = "Scheduling",
                Description = $"{timeOfDayPerformance.Period} period shows high prediction errors ({timeOfDayPerformance.AvgError:F2}s). Adjust time-of-day factors.",
                Priority = 0.5,
                ExpectedImprovement = 15,
                Parameters = new Dictionary<string, object>
                {
                    ["TimeOfDay"] = timeOfDayPerformance.Period.ToString(),
                    ["AverageError"] = timeOfDayPerformance.AvgError,
                    ["SuggestedFactor"] = 1.0 + (timeOfDayPerformance.AvgError / 10.0)
                },
                GeneratedAt = DateTime.UtcNow
            });
        }

        return recommendations.OrderByDescending(r => r.Priority).ToList();
    }

    /// <summary>
    /// Get overall system health score (0-100)
    /// </summary>
    public double GetHealthScore()
    {
        var observations = _observations.ToArray();
        
        if (observations.Length < MinSamplesForAnalysis)
            return 50.0; // Neutral when not enough data

        var recentObservations = observations.TakeLast(100).ToArray();
        
        // Weighted health metrics
        var accuracyScore = recentObservations.Average(o => o.PredictionAccuracy) * 40; // 40% weight
        var reliabilityScore = recentObservations.Count(o => Math.Abs(o.PredictionError) < o.ActualTime * 0.1) / (double)recentObservations.Length * 30; // 30% weight
        var energyScore = (1.0 - recentObservations.Average(o => Math.Abs(o.EnergyPredictionError))) * 30; // 30% weight

        return Math.Clamp(accuracyScore + reliabilityScore + energyScore, 0, 100);
    }

    private void UpdateZoneStatistics(PerformanceObservation observation)
    {
        lock (_lock)
        {
            var zones = new[] { observation.OriginZone, observation.DestinationZone };
            
            foreach (var zoneId in zones.Where(z => !string.IsNullOrEmpty(z)))
            {
                if (!_zoneStats.ContainsKey(zoneId))
                {
                    _zoneStats[zoneId] = new ZonePerformanceStats
                    {
                        ZoneId = zoneId,
                        TimeOfDayBreakdown = Enum.GetValues<TimeOfDay>()
                            .Select(t => new TimeOfDayStats { Period = t })
                            .ToList()
                    };
                }

                // Update zone statistics (simplified - would be more sophisticated in production)
                var stats = _zoneStats[zoneId];
                var newTotalTransits = stats.TotalTransits + 1;
                var newAvgTime = (stats.AverageTransitTime * stats.TotalTransits + observation.ActualTime) / newTotalTransits;
                var newAvgDelay = (stats.AverageDelay * stats.TotalTransits + observation.PredictionError) / newTotalTransits;
                
                var observations = _observations.Where(o => 
                    o.OriginZone == zoneId || o.DestinationZone == zoneId).ToList();
                var reliable = observations.Count(o => Math.Abs(o.PredictionError) < o.ActualTime * 0.1);
                
                _zoneStats[zoneId] = stats with
                {
                    TotalTransits = newTotalTransits,
                    AverageTransitTime = newAvgTime,
                    AverageDelay = newAvgDelay,
                    Reliability = observations.Any() ? reliable / (double)observations.Count : 1.0,
                    LastUpdated = DateTime.UtcNow
                };
            }
        }
    }

    private static double CalculateTrend(PerformanceObservation[] observations, Func<PerformanceObservation, double> metricSelector)
    {
        if (observations.Length < 2)
            return 0;

        // Simple linear regression
        var n = observations.Length;
        var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
        var y = observations.Select(metricSelector).ToArray();

        var xMean = x.Average();
        var yMean = y.Average();

        var numerator = x.Zip(y, (xi, yi) => (xi - xMean) * (yi - yMean)).Sum();
        var denominator = x.Select(xi => Math.Pow(xi - xMean, 2)).Sum();

        return denominator > 0 ? numerator / denominator : 0;
    }

    /// <summary>
    /// Export analytics report
    /// </summary>
    public string GenerateAnalyticsReport()
    {
        var observations = _observations.ToArray();
        var report = new System.Text.StringBuilder();

        report.AppendLine("=== FMS PERFORMANCE ANALYTICS REPORT ===");
        report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        report.AppendLine($"Total Observations: {observations.Length}");
        report.AppendLine($"System Health Score: {GetHealthScore():F1}/100");
        report.AppendLine();

        if (observations.Length >= MinSamplesForAnalysis)
        {
            var accuracyTrend = AnalyzeTrend(o => o.PredictionAccuracy, "PredictionAccuracy");
            report.AppendLine("--- PREDICTION ACCURACY ---");
            report.AppendLine($"Mean: {accuracyTrend.Mean:P2}");
            report.AppendLine($"Std Dev: {accuracyTrend.StandardDeviation:F4}");
            report.AppendLine($"Trend: {(accuracyTrend.Trend > 0 ? "IMPROVING" : "DEGRADING")} ({accuracyTrend.Trend:F6})");
            report.AppendLine();

            report.AppendLine("--- TOP PROBLEMATIC ZONES ---");
            var problematicZones = _zoneStats
                .OrderBy(z => z.Value.Reliability)
                .Take(5);
            
            foreach (var zone in problematicZones)
            {
                report.AppendLine($"{zone.Key}: Reliability={zone.Value.Reliability:P0}, AvgDelay={zone.Value.AverageDelay:F2}s, Transits={zone.Value.TotalTransits}");
            }
            report.AppendLine();

            report.AppendLine("--- IMPROVEMENT RECOMMENDATIONS ---");
            var recommendations = GenerateRecommendations();
            foreach (var rec in recommendations.Take(5))
            {
                report.AppendLine($"[{rec.Category}] Priority={rec.Priority:F2}: {rec.Description}");
            }
        }
        else
        {
            report.AppendLine("Insufficient data for detailed analysis.");
        }

        return report.ToString();
    }
}
