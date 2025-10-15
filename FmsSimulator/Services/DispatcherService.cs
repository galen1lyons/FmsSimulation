using FmsSimulator.Models;

namespace FmsSimulator.Services;

// This service's single responsibility is to dispatch orders.
// It acts as the bridge between our FMS logic and the communication library.
public class DispatcherService
{
    // The dispatcher owns an instance of our Jules client.
    private readonly JulesMqttClient _mqttClient = new();

    // The 'async' keyword is needed here because it calls another 'async' method.
    public async Task DispatchOrderAsync(AssignmentPlan winningPlan)
    {
        Console.WriteLine($"✅ DISPATCHING: Sending AMR {winningPlan.AssignedAmr.Id} to {winningPlan.Task.ToLocation}.");
        await _mqttClient.PublishAsync(winningPlan);
    }
}