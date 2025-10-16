using FmsSimulator.Models;
using Microsoft.Extensions.Logging;
using static FmsSimulator.Services.IFmsServices;

namespace FmsSimulator.Services;

public class AmrInternalController : ICommunicationService
{
    private readonly string _amrId;
    private readonly LoggingService _logger = LoggingService.Instance;

    public AmrInternalController(string amrId)
    {
        _amrId = amrId;
    }

    public async Task PublishVda5050OrderAsync(AssignmentPlan plan)
    {
        try
        {
            _logger.LogOperationalMetrics("AmrController", "PublishVda5050Order", new Dictionary<string, object>
            {
                ["amrId"] = _amrId,
                ["orderId"] = plan.Task.TaskId,
                ["fromLocation"] = plan.Task.FromLocation,
                ["toLocation"] = plan.Task.ToLocation
            });

            await Task.Delay(100); // Simulate network latency
        }
        catch (Exception ex)
        {
            _logger.LogError("AmrController", "PublishVda5050Order", ex);
            throw;
        }
    }

    public async Task PublishInternalCommandAsync(string topic, string payload)
    {
        try
        {
            _logger.LogOperationalMetrics("AmrController", "PublishInternalCommand", new Dictionary<string, object>
            {
                ["amrId"] = _amrId,
                ["topic"] = topic,
                ["payload"] = payload,
                ["timestamp"] = DateTime.UtcNow
            });

            await Task.Delay(50); // Simulate command processing
        }
        catch (Exception ex)
        {
            _logger.LogError("AmrController", "PublishInternalCommand", ex);
            throw;
        }
    }

    public async Task ProcessVda5050Order(ProductionTask order)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogOperationalMetrics("AmrController", "ProcessOrder", new Dictionary<string, object>
            {
                ["amrId"] = _amrId,
                ["orderId"] = order.TaskId,
                ["operation"] = "start"
            });

            // 1. Navigate to pickup location
            await PublishInternalCommandAsync("amr/internal/navigation/command", $"GoTo:{order.FromLocation}");

            // 2. Execute module-specific action
            if (order.RequiredModule == "Electric AGV Lift")
            {
                await PublishInternalCommandAsync("amr/internal/lift/command", $"ExecuteLift:{order.RequiredLiftHeight}");
            }
            else if (order.RequiredModule == "6-Axis Robotic Arm")
            {
                await PublishInternalCommandAsync("amr/internal/arm/command", $"ExecutePick");
            }

            // 3. Navigate to delivery location
            await PublishInternalCommandAsync("amr/internal/navigation/command", $"GoTo:{order.ToLocation}");

            stopwatch.Stop();
            _logger.LogOperationalMetrics("AmrController", "ProcessOrder", new Dictionary<string, object>
            {
                ["amrId"] = _amrId,
                ["orderId"] = order.TaskId,
                ["operation"] = "complete",
                ["processingTime"] = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("AmrController", "ProcessVda5050Order", ex);
            throw;
        }
    }
}