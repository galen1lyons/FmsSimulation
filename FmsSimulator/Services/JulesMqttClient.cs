using FmsSimulator.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FmsSimulator.Services;

// This class simulates our third-party connectivity library, "Jules".
public class JulesMqttClient : IJulesMqttClient
{
    private readonly ILogger<JulesMqttClient> _logger;

    public JulesMqttClient(ILogger<JulesMqttClient> logger)
    {
        _logger = logger;
    }

    // A real network call isn't instant, so we'll simulate that.
    // The 'async Task' keywords are how C# handles operations that take time.
    public async Task PublishAsync(AssignmentPlan winningPlan)
    {
        _logger.LogInformation("\n   [Jules]: Establishing connection to MQTT broker...");
        await Task.Delay(200); // Simulate network latency.

        // Create the message payload.
        var payload = JsonSerializer.Serialize(new
        {
            AmrId = winningPlan.AssignedAmr.Id,
            TaskId = winningPlan.Task.TaskId,
            Destination = winningPlan.Task.ToLocation
        });

        // "Publish" the message.
        _logger.LogInformation("   [Jules]: PUBLISHING to topic 'vda5050/{AmrId}/order'", winningPlan.AssignedAmr.Id);
        _logger.LogInformation("   [Jules]: Payload: {Payload}", payload);
        await Task.Delay(100); // Simulate publish confirmation time.
        _logger.LogInformation("   [Jules]: Message published successfully.");
    }
}