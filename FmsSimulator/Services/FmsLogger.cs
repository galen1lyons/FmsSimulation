using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FmsSimulator.Services;

/// <summary>
/// Provides structured logging helpers for tri-planar communication in the FMS.
/// </summary>
public class FmsLogger
{
    private readonly ILogger _logger;
    private readonly string _component;

    public FmsLogger(ILogger logger, string component)
    {
        _logger = logger;
        _component = component;
    }

    // Vertical Communication (ISA-95) Logging

    public void LogOrderLifecycle(string orderId, string status, object? details = null)
    {
        var logMessage = new Dictionary<string, object>
        {
            ["plane"] = "vertical",
            ["protocol"] = "ISA-95",
            ["component"] = _component,
            ["orderId"] = orderId,
            ["status"] = status,
            ["timestamp"] = DateTime.UtcNow
        };

        if (details != null)
        {
            logMessage["details"] = details;
        }

        _logger.LogInformation("ISA-95 Order {OrderId}: {Status} | {Details}",
            orderId, status, JsonSerializer.Serialize(logMessage));
    }

    public void LogResourceAllocation(string orderId, string resourceId, string status)
    {
        var logMessage = new Dictionary<string, object>
        {
            ["plane"] = "vertical",
            ["protocol"] = "ISA-95",
            ["component"] = _component,
            ["orderId"] = orderId,
            ["resourceId"] = resourceId,
            ["status"] = status,
            ["timestamp"] = DateTime.UtcNow
        };

        _logger.LogInformation("ISA-95 Resource Allocation: Order {OrderId}, Resource {ResourceId}: {Status} | {Details}",
            orderId, resourceId, status, JsonSerializer.Serialize(logMessage));
    }

    // Horizontal Communication (VDA 5050) Logging

    public void LogVdaMessage(string amrId, string messageType, string status, object payload)
    {
        var logMessage = new Dictionary<string, object>
        {
            ["plane"] = "horizontal",
            ["protocol"] = "VDA 5050",
            ["component"] = _component,
            ["amrId"] = amrId,
            ["messageType"] = messageType,
            ["status"] = status,
            ["payload"] = payload,
            ["timestamp"] = DateTime.UtcNow
        };

        _logger.LogInformation("VDA 5050 {MessageType}: AMR {AmrId} - {Status} | {Details}",
            messageType, amrId, status, JsonSerializer.Serialize(logMessage));
    }

    public void LogNodeExecution(string amrId, string nodeId, string status, TimeSpan? duration = null)
    {
        var logMessage = new Dictionary<string, object>
        {
            ["plane"] = "horizontal",
            ["protocol"] = "VDA 5050",
            ["component"] = _component,
            ["amrId"] = amrId,
            ["nodeId"] = nodeId,
            ["status"] = status,
            ["timestamp"] = DateTime.UtcNow
        };

        if (duration.HasValue)
        {
            logMessage["duration_ms"] = duration.Value.TotalMilliseconds;
        }

        _logger.LogInformation("VDA 5050 Node Execution: AMR {AmrId}, Node {NodeId}: {Status} | {Details}",
            amrId, nodeId, status, JsonSerializer.Serialize(logMessage));
    }

    // Internal Communication (MQTT) Logging

    public void LogMqttCommand(string amrId, string topic, object payload, int qosLevel, string correlationId)
    {
        var logMessage = new Dictionary<string, object>
        {
            ["plane"] = "internal",
            ["protocol"] = "MQTT",
            ["component"] = _component,
            ["amrId"] = amrId,
            ["topic"] = topic,
            ["payload"] = payload,
            ["qosLevel"] = qosLevel,
            ["correlationId"] = correlationId,
            ["timestamp"] = DateTime.UtcNow
        };

        _logger.LogInformation("MQTT Command: AMR {AmrId}, Topic {Topic}, QoS {QosLevel} | {Details}",
            amrId, topic, qosLevel, JsonSerializer.Serialize(logMessage));
    }

    public void LogMqttResponse(string amrId, string correlationId, string status, TimeSpan processingTime)
    {
        var logMessage = new Dictionary<string, object>
        {
            ["plane"] = "internal",
            ["protocol"] = "MQTT",
            ["component"] = _component,
            ["amrId"] = amrId,
            ["correlationId"] = correlationId,
            ["status"] = status,
            ["processingTime_ms"] = processingTime.TotalMilliseconds,
            ["timestamp"] = DateTime.UtcNow
        };

        _logger.LogInformation("MQTT Response: AMR {AmrId}, CorrelationId {CorrelationId}: {Status} ({ProcessingTime:N0}ms) | {Details}",
            amrId, correlationId, status, processingTime.TotalMilliseconds, JsonSerializer.Serialize(logMessage));
    }

    // Performance Monitoring

    public void LogPerformanceMetric(string metricName, double value, string unit, Dictionary<string, string>? tags = null)
    {
        var logMessage = new Dictionary<string, object>
        {
            ["type"] = "metric",
            ["component"] = _component,
            ["metric"] = metricName,
            ["value"] = value,
            ["unit"] = unit,
            ["timestamp"] = DateTime.UtcNow
        };

        if (tags != null)
        {
            logMessage["tags"] = tags;
        }

        _logger.LogInformation("Performance Metric: {Metric} = {Value}{Unit} | {Details}",
            metricName, value, unit, JsonSerializer.Serialize(logMessage));
    }

    // Error Logging

    public void LogProtocolError(string plane, string protocol, string errorType, string message, Exception? ex = null)
    {
        var logMessage = new Dictionary<string, object>
        {
            ["plane"] = plane,
            ["protocol"] = protocol,
            ["component"] = _component,
            ["errorType"] = errorType,
            ["message"] = message,
            ["timestamp"] = DateTime.UtcNow
        };

        if (ex != null)
        {
            logMessage["exception"] = new
            {
                ex.Message,
                ex.StackTrace,
                ex.Source
            };
        }

        _logger.LogError("Protocol Error: {Protocol} on {Plane} plane - {ErrorType}: {Message} | {Details}",
            protocol, plane, errorType, message, JsonSerializer.Serialize(logMessage));
    }
}