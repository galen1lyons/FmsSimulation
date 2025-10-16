using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FmsSimulator.Services;

public sealed class LoggingService
{
    private static readonly LoggingService _instance = new();
    private readonly ILogger<LoggingService> _logger;
    
    public static LoggingService Instance => _instance;
    
    private LoggingService()
    {
        var factory = LoggerFactory.Create(builder =>
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information));
        _logger = factory.CreateLogger<LoggingService>();
    }

    public void LogOperationalMetrics(string component, string operation, Dictionary<string, object> metrics)
    {
        var logEntry = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow,
            ["component"] = component,
            ["operation"] = operation,
            ["metrics"] = metrics
        };

        _logger.LogInformation(JsonSerializer.Serialize(logEntry));
    }

    public void LogError(string component, string operation, Exception ex)
    {
        _logger.LogError(ex, "[{Component}] {Operation} failed: {Message}", component, operation, ex.Message);
    }
    
    public void LogPerformanceMetric(string component, string metric, double value)
    {
        _logger.LogInformation("[{Component}] {Metric}: {Value}", component, metric, value);
    }
}