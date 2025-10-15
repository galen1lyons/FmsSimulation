namespace FmsSimulator.Models;

// Represents a high-level order from an Enterprise Resource Planning (ERP)
// or Manufacturing Execution System (MES), as defined by ISA-95.
public class ProductionOrder
{
    public string OrderId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty; // Stock Keeping Unit (e.g., "PALLET-A5")
    public int Quantity { get; set; } = 0;
    public string SourceLocation { get; set; } = string.Empty;
    public string DestinationLocation { get; set; } = string.Empty;
}