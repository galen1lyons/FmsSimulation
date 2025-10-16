using System.ComponentModel.DataAnnotations;

namespace FmsSimulator.Models.ISA95;

public class ProductionOrder
{
    public required string OrderId { get; set; }
    public required string Site { get; set; }
    public required string Area { get; set; }
    public required string WorkCenter { get; set; }
    public required ProductionSchedule Schedule { get; set; }
    public required List<MaterialRequirement> Materials { get; set; }
    public required OperationsDefinition Operations { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;
}

public class ProductionSchedule
{
    public required DateTime StartTime { get; set; }
    public required DateTime EndTime { get; set; }
    public required string Priority { get; set; }
}

public class MaterialRequirement
{
    public required string MaterialId { get; set; }
    public required string Description { get; set; }
    public required double Quantity { get; set; }
    public required string Unit { get; set; }
    public required string Location { get; set; }
}

public class OperationsDefinition
{
    public required List<OperationStep> Steps { get; set; }
    public required string WorkflowTemplate { get; set; }
}

public class OperationStep
{
    public required string StepId { get; set; }
    public required string Description { get; set; }
    public required TimeSpan EstimatedDuration { get; set; }
    public required List<string> RequiredResources { get; set; }
}

public enum OrderStatus
{
    Created,
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    OnHold
}