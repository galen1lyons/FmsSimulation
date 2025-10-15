using FmsSimulator.Models;
using Microsoft.Extensions.Logging;

namespace FmsSimulator.Services;

// This service's single responsibility is to dispatch orders.
// It acts as the bridge between our FMS logic and the communication library.
public class DispatcherService : IDispatcherService
{
    private readonly IJulesMqttClient _mqttClient;
    private readonly ILogger<DispatcherService> _logger;

    public DispatcherService(IJulesMqttClient mqttClient, ILogger<DispatcherService> logger)
    {
        _mqttClient = mqttClient;
        _logger = logger;
    }

    // The 'async' keyword is needed here because it calls another 'async' method.
    public async Task DispatchOrderAsync(AssignmentPlan winningPlan)
    {
        _logger.LogInformation("✅ DISPATCHING: Sending AMR {AmrId} to {Location}.", winningPlan.AssignedAmr.Id, winningPlan.Task.ToLocation);
        await _mqttClient.PublishAsync(winningPlan);
    }
}