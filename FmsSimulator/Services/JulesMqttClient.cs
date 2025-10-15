using FmsSimulator.Models;
using System.Text.Json;

namespace FmsSimulator.Services;

// This class simulates our third-party connectivity library, "Jules".
public class JulesMqttClient
{
    // A real network call isn't instant, so we'll simulate that.
    // The 'async Task' keywords are how C# handles operations that take time.
    public async Task PublishAsync(AssignmentPlan winningPlan)
    {
        Console.WriteLine("\n   [Jules]: Establishing connection to MQTT broker...");
        await Task.Delay(200); // Simulate network latency.

        // Create the message payload.
        var payload = JsonSerializer.Serialize(new {
            AmrId = winningPlan.AssignedAmr.Id,
            TaskId = winningPlan.Task.TaskId,
            Destination = winningPlan.Task.ToLocation
        });

        // "Publish" the message.
        Console.WriteLine($"   [Jules]: PUBLISHING to topic 'vda5050/{winningPlan.AssignedAmr.Id}/order'");
        Console.WriteLine($"   [Jules]: Payload: {payload}");
        await Task.Delay(100); // Simulate publish confirmation time.
        Console.WriteLine("   [Jules]: Message published successfully.");
    }
}