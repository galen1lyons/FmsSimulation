using System.ComponentModel.DataAnnotations;

namespace FmsSimulator.Models.VDA5050;

public class Vda5050Message
{
    public required Header Header { get; set; }
    public required Order Order { get; set; }
    public required List<Node> Nodes { get; set; }
    public required List<Edge> Edges { get; set; }
}

public class Header
{
    public required string HeaderId { get; set; }
    public required string Timestamp { get; set; }
    public required string Version { get; set; } = "2.0";
    public required string Manufacturer { get; set; }
    public required string SerialNumber { get; set; }
}

public class Order
{
    public required string OrderId { get; set; }
    public required string OrderUpdateId { get; set; }
    public required int ZoneSetId { get; set; }
    public required OrderStatus OrderStatus { get; set; }
}

public class Node
{
    public required string NodeId { get; set; }
    public required double X { get; set; }
    public required double Y { get; set; }
    public List<NodeAction> Actions { get; set; } = new();
}

public class Edge
{
    public required string EdgeId { get; set; }
    public required string StartNodeId { get; set; }
    public required string EndNodeId { get; set; }
    public required double Length { get; set; }
    public required string Direction { get; set; }
    public required double MaxSpeed { get; set; }
}

public class NodeAction
{
    public required string ActionType { get; set; }
    public required string ActionId { get; set; }
    public Dictionary<string, object> ActionParameters { get; set; } = new();
}

public enum OrderStatus
{
    PENDING,
    ACCEPTED,
    REJECTED,
    ONGOING,
    CANCELED,
    FINISHED,
    FAILED
}