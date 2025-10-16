using FmsSimulator.Models;
using Microsoft.Extensions.Logging;
using static FmsSimulator.Services.IFmsServices;

namespace FmsSimulator.Services;

public class ErpConnectorService : IErpConnector
{
    private readonly LoggingService _logger = LoggingService.Instance;

    // In a real system, this would be a database lookup.
    // Here, we simulate a product database to find handling requirements.
    private readonly Dictionary<string, (double weight, string module, double liftHeight)> _skuDatabase = new()
    {
        { "HEAVY-PALLET-A", (1200, "Electric AGV Lift", 500) },
        { "SMALL-COMPONENT-B", (50, "6-Axis Robotic Arm", 0) },
        { "MEDIUM-PALLET-C", (900, "Electric AGV Lift", 200) },
        { "HEAVY-PALLET-D", (1300, "Electric AGV Lift", 400) }
    };

    // This method simulates fetching orders from the ERP and translating them.
    public Queue<ProductionTask> FetchAndTranslateOrders()
    {
        try
        {
            _logger.LogOperationalMetrics("ErpConnector", "FetchOrders", new Dictionary<string, object>
            {
                ["operation"] = "start",
                ["timestamp"] = DateTime.UtcNow
            });

            // 1. Simulate fetching a list of high-level orders.
            var erpOrders = new List<ProductionOrder>
            {
                new() { OrderId = "PO-001", Sku = "HEAVY-PALLET-A", SourceLocation = "Dock A", DestinationLocation = "Assembly B" },
                new() { OrderId = "PO-002", Sku = "SMALL-COMPONENT-B", SourceLocation = "Warehouse", DestinationLocation = "QC Station" },
                new() { OrderId = "PO-003", Sku = "MEDIUM-PALLET-C", SourceLocation = "Staging Area", DestinationLocation = "Warehouse Rack 12" },
                new() { OrderId = "PO-004", Sku = "HEAVY-PALLET-D", SourceLocation = "Dock B", DestinationLocation = "Shipping Dock" }
            };

            var fmsTaskQueue = new Queue<ProductionTask>();

            // 2. Translate each ERP Order into a detailed FMS Task.
            foreach (var order in erpOrders)
            {
                if (_skuDatabase.TryGetValue(order.Sku, out var details))
                {
                    var newTask = new ProductionTask
                    {
                        TaskId = order.OrderId,
                        FromLocation = order.SourceLocation,
                        ToLocation = order.DestinationLocation,
                        RequiredPayload = details.weight,
                        RequiredModule = details.module,
                        RequiredLiftHeight = details.liftHeight
                    };
                    fmsTaskQueue.Enqueue(newTask);

                    _logger.LogOperationalMetrics("ErpConnector", "TaskTranslation", new Dictionary<string, object>
                    {
                        ["orderId"] = order.OrderId,
                        ["sku"] = order.Sku,
                        ["taskId"] = newTask.TaskId
                    });
                }
            }

            return fmsTaskQueue;
        }
        catch (Exception ex)
        {
            _logger.LogError("ErpConnector", "FetchAndTranslateOrders", ex);
            throw;
        }
    }
}