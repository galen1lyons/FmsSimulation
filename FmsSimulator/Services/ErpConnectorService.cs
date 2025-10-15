using FmsSimulator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FmsSimulator.Services;

public class ErpConnectorService
{
    private readonly ILogger<ErpConnectorService> _logger;
    private readonly Dictionary<string, (double weight, string module, double liftHeight)> _skuDatabase;

    public ErpConnectorService(ILogger<ErpConnectorService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _skuDatabase = configuration.GetSection("FmsSettings:SkuDatabase")
            .GetChildren()
            .ToDictionary(
                x => x.Key,
                x => (
                    x.GetValue<double>("Weight"),
                    x.GetValue<string>("Module") ?? "",
                    x.GetValue<double>("LiftHeight")
                )
            );
    }

    // This method simulates fetching orders from the ERP and translating them.
    public Queue<ProductionTask> FetchAndTranslateOrders()
    {
        _logger.LogInformation("[ERP Connector]: Fetching new production orders...");

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
                _logger.LogInformation("[ERP Connector]: Order {OrderId} translated to FMS task.", order.OrderId);
            }
        }

        return fmsTaskQueue;
    }
}