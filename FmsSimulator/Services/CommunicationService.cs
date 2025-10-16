using FmsSimulator.Models;
using System.Text.Json;
using static FmsSimulator.Services.IFmsServices;

namespace FmsSimulator.Services;

public class CommunicationService : ICommunicationService
{
    private readonly string _amrId;
    private readonly LoggingService _logger = LoggingService.Instance;
    private readonly Dictionary<string, Func<string, Task>> _commandHandlers;

    public CommunicationService(string amrId)
    {
        _amrId = amrId;
        _commandHandlers = new Dictionary<string, Func<string, Task>>
        {
            ["navigation"] = HandleNavigationCommand,
            ["lift"] = HandleLiftCommand,
            ["arm"] = HandleArmCommand
        };
    }

    public async Task PublishVda5050OrderAsync(AssignmentPlan plan)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Simulate network latency
            await Task.Delay(100);
            
            var message = new
            {
                plan.AssignedAmr.Id,
                plan.Task.FromLocation,
                plan.Task.ToLocation,
                Timestamp = DateTime.UtcNow
            };

            stopwatch.Stop();
            _logger.LogOperationalMetrics("CommunicationService", "PublishVda5050Order", new Dictionary<string, object>
            {
                ["publishTime"] = stopwatch.ElapsedMilliseconds,
                ["messageSize"] = JsonSerializer.Serialize(message).Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("CommunicationService", "PublishVda5050Order", ex);
            throw;
        }
    }

    public async Task PublishInternalCommandAsync(string topic, string payload)
    {
        try
        {
            var parts = topic.Split('/');
            var commandType = parts[2]; // e.g., "amr/internal/navigation" -> "navigation"
            if (_commandHandlers.TryGetValue(commandType, out var handler))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try 
                {
                    await handler(payload);
                    sw.Stop();
                    _logger.LogOperationalMetrics("CommunicationService", $"CommandLatency_{commandType}", new Dictionary<string, object>
                    {
                        ["latencyMs"] = sw.ElapsedMilliseconds,
                        ["success"] = true
                    });
                }
                catch (Exception handlerEx)
                {
                    sw.Stop();
                    _logger.LogOperationalMetrics("CommunicationService", $"CommandLatency_{commandType}", new Dictionary<string, object>
                    {
                        ["latencyMs"] = sw.ElapsedMilliseconds,
                        ["success"] = false,
                        ["errorType"] = handlerEx.GetType().Name
                    });
                    throw;
                }
            }
            else
            {
                _logger.LogError("CommunicationService", $"PublishInternal_{topic}", 
                    new InvalidOperationException($"No handler found for command type: {commandType}"));
                throw new InvalidOperationException($"No handler found for command type: {commandType}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("CommunicationService", $"PublishInternal_{topic}", ex);
            throw;
        }
    }

    public async Task ProcessVda5050Order(ProductionTask order)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var latencyStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            await PublishInternalCommandAsync("amr/internal/navigation", $"GoTo:{order.FromLocation}");
            var navigationLatency = latencyStopwatch.ElapsedMilliseconds;
            latencyStopwatch.Restart();

            if (order.RequiredModule == "Electric AGV Lift")
            {
                await PublishInternalCommandAsync("amr/internal/lift/command", $"ExecuteLift:{order.RequiredLiftHeight}");
            }

            stopwatch.Stop();
            _logger.LogOperationalMetrics("CommunicationService", "ProcessVda5050Order", new Dictionary<string, object>
            {
                ["totalProcessingTime"] = stopwatch.ElapsedMilliseconds,
                ["navigationLatency"] = navigationLatency,
                ["orderId"] = order.TaskId,
                ["commandCount"] = order.RequiredModule == "Electric AGV Lift" ? 2 : 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("CommunicationService", "ProcessVda5050Order", ex);
            throw;
        }
    }

    private async Task HandleNavigationCommand(string payload)
    {
        var parts = payload.Split(':');
        if (parts.Length < 2 || !parts[0].Equals("GoTo", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid navigation command format. Expected 'GoTo:location', got '{payload}'");
        }

        var location = parts[1];
        _logger.LogOperationalMetrics("CommunicationService", "Navigation", new Dictionary<string, object>
        {
            ["amrId"] = _amrId,
            ["targetLocation"] = location,
            ["command"] = parts[0]
        });
        await Task.Delay(50); // Simulate command processing
    }

    private async Task HandleLiftCommand(string payload)
    {
        var parts = payload.Split(':');
        if (parts.Length < 2 || !double.TryParse(parts[1], out var height))
        {
            throw new ArgumentException($"Invalid lift command format. Expected 'SetHeight:number', got '{payload}'");
        }

        _logger.LogOperationalMetrics("CommunicationService", "Lift", new Dictionary<string, object>
        {
            ["amrId"] = _amrId,
            ["liftHeight"] = height,
            ["command"] = parts[0]
        });
        await Task.Delay(50);
    }

    private async Task HandleArmCommand(string payload)
    {
        var parts = payload.Split(':');
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid arm command format. Expected 'command:parameter', got '{payload}'");
        }

        _logger.LogOperationalMetrics("CommunicationService", "Arm", new Dictionary<string, object>
        {
            ["amrId"] = _amrId,
            ["command"] = parts[0],
            ["parameter"] = parts[1]
        });
        await Task.Delay(50);
    }
}