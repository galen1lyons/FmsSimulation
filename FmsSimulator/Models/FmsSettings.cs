using System.ComponentModel.DataAnnotations;

namespace FmsSimulator.Models;

public class FmsSettings
{
    [Required]
    public required TargetPosition TargetPosition { get; set; }

    [Required]
    public required Dictionary<string, SkuInfo> SkuDatabase { get; set; }

    [Required]
    public required string TrafficDataPath { get; set; }
}

public class TargetPosition
{
    [Range(-1000, 1000)]
    public required int X { get; set; }

    [Range(-1000, 1000)]
    public required int Y { get; set; }
}

public class SkuInfo
{
    [Range(0, double.MaxValue)]
    public required double Weight { get; set; }

    [Required]
    public required string Module { get; set; }

    [Range(0, double.MaxValue)]
    public required double LiftHeight { get; set; }
}