using FmsSimulator.Models;

namespace FmsSimulator.Services;

public class AmrInternalController
{
    private readonly string _amrId;

    public AmrInternalController(string amrId)
    {
        _amrId = amrId;
    }

    // This method is called when the AMR receives a VDA 5050 order from the FMS.
    public async Task ProcessVda5050Order(ProductionTask order)
    {
        Console.WriteLine($"   [{_amrId} Internal]: VDA 5050 Order '{order.TaskId}' received. Processing sequence...");
        
        // --- Translate VDA 5050 to Internal Commands ---

        // 1. Command to navigate to the 'From' location.
        await PublishInternalCommandAsync("amr/internal/navigation/command", $"GoTo:{order.FromLocation}");

        // 2. Command for the specific module action (e.g., lift, pick).
        if (order.RequiredModule == "Electric AGV Lift")
        {
            await PublishInternalCommandAsync("amr/internal/lift/command", $"ExecuteLift:{order.RequiredLiftHeight}");
        }
        else if (order.RequiredModule == "6-Axis Robotic Arm")
        {
            await PublishInternalCommandAsync("amr/internal/arm/command", "ExecutePick");
        }

        // 3. Command to navigate to the 'To' location.
        await PublishInternalCommandAsync("amr/internal/navigation/command", $"GoTo:{order.ToLocation}");
        
        // 4. Command for the final drop-off action.
        // (Simplified for this simulation)
        
        Console.WriteLine($"   [{_amrId} Internal]: Sequence for Order '{order.TaskId}' complete.");
    }

    // This simulates publishing a message to the AMR's own internal MQTT bus.
    private async Task PublishInternalCommandAsync(string topic, string payload)
    {
        var message = new InternalAmrMessage { Topic = topic, Payload = payload };
        Console.WriteLine($"   [{_amrId} Internal]: MQTT PUBLISH -> Topic: '{message.Topic}', Payload: '{message.Payload}'");
        await Task.Delay(150); // Simulate processing time for the component.
    }
}