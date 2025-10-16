using System.ComponentModel.DataAnnotations;
using MQTTnet.Protocol;

namespace FmsSimulator.Models;

/// <summary>
/// MQTT Broker configuration for production deployment
/// Phase 1: Communication Backbone Infrastructure
/// </summary>
public class MqttBrokerSettings
{
    [Required]
    public string BrokerHost { get; set; } = "localhost";
    
    [Range(1, 65535)]
    public int BrokerPort { get; set; } = 1883;
    
    public bool UseTls { get; set; } = false;
    
    public int TlsPort { get; set; } = 8883;
    
    public string? Username { get; set; }
    
    public string? Password { get; set; }
    
    public string ClientId { get; set; } = $"FMS_{Environment.MachineName}_{Guid.NewGuid():N}";
    
    /// <summary>
    /// Clean session: false for persistent sessions, true for clean start
    /// </summary>
    public bool CleanSession { get; set; } = false;
    
    /// <summary>
    /// Keep-alive interval in seconds
    /// </summary>
    [Range(10, 300)]
    public int KeepAliveSeconds { get; set; } = 60;
    
    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Range(5, 60)]
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Automatic reconnection enabled
    /// </summary>
    public bool AutoReconnect { get; set; } = true;
    
    /// <summary>
    /// Reconnection delay in seconds
    /// </summary>
    [Range(1, 60)]
    public int ReconnectDelaySeconds { get; set; } = 5;
    
    /// <summary>
    /// Maximum reconnection attempts (0 = infinite)
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 0;
    
    /// <summary>
    /// Default Quality of Service level
    /// </summary>
    public MqttQualityOfServiceLevel DefaultQoS { get; set; } = MqttQualityOfServiceLevel.AtLeastOnce;
    
    /// <summary>
    /// Message retention enabled
    /// </summary>
    public bool RetainMessages { get; set; } = false;
}

/// <summary>
/// VDA 5050 Topic configuration
/// </summary>
public class Vda5050TopicSettings
{
    /// <summary>
    /// Base topic prefix (e.g., "vda5050/v2")
    /// </summary>
    [Required]
    public string BaseTopicPrefix { get; set; } = "vda5050/v2";
    
    /// <summary>
    /// Manufacturer identifier
    /// </summary>
    [Required]
    public string Manufacturer { get; set; } = "FMS";
    
    /// <summary>
    /// Serial number for this FMS instance
    /// </summary>
    public string SerialNumber { get; set; } = Environment.MachineName;
    
    /// <summary>
    /// Order topic template: {basePrefix}/{manufacturer}/{serialNumber}/order
    /// </summary>
    public string GetOrderTopic(string amrId) => 
        $"{BaseTopicPrefix}/{Manufacturer}/{amrId}/order";
    
    /// <summary>
    /// Instant actions topic
    /// </summary>
    public string GetInstantActionsTopic(string amrId) => 
        $"{BaseTopicPrefix}/{Manufacturer}/{amrId}/instantActions";
    
    /// <summary>
    /// State topic (AMR publishes here)
    /// </summary>
    public string GetStateTopic(string amrId) => 
        $"{BaseTopicPrefix}/{Manufacturer}/{amrId}/state";
    
    /// <summary>
    /// Visualization topic
    /// </summary>
    public string GetVisualizationTopic(string amrId) => 
        $"{BaseTopicPrefix}/{Manufacturer}/{amrId}/visualization";
    
    /// <summary>
    /// Connection state topic
    /// </summary>
    public string GetConnectionTopic(string amrId) => 
        $"{BaseTopicPrefix}/{Manufacturer}/{amrId}/connection";
    
    /// <summary>
    /// Wildcard subscription for all AMR states
    /// </summary>
    public string GetStateWildcardTopic() => 
        $"{BaseTopicPrefix}/{Manufacturer}/+/state";
}

/// <summary>
/// High-availability configuration
/// </summary>
public class MqttHighAvailabilitySettings
{
    /// <summary>
    /// Enable connection pooling
    /// </summary>
    public bool EnableConnectionPool { get; set; } = true;
    
    /// <summary>
    /// Pool size (number of connections)
    /// </summary>
    [Range(1, 10)]
    public int PoolSize { get; set; } = 3;
    
    /// <summary>
    /// Enable message persistence to disk
    /// </summary>
    public bool EnableMessagePersistence { get; set; } = true;
    
    /// <summary>
    /// Persistence directory path
    /// </summary>
    public string PersistenceDirectory { get; set; } = "./mqtt_persistence";
    
    /// <summary>
    /// Maximum messages to persist
    /// </summary>
    public int MaxPersistedMessages { get; set; } = 10000;
    
    /// <summary>
    /// Enable circuit breaker pattern
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;
    
    /// <summary>
    /// Circuit breaker failure threshold
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;
    
    /// <summary>
    /// Circuit breaker timeout in seconds
    /// </summary>
    public int CircuitBreakerTimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Complete MQTT configuration including broker, VDA 5050, and high-availability settings
/// </summary>
public class MqttSettings
{
    public MqttBrokerSettings MqttBroker { get; set; } = new();
    public Vda5050TopicSettings Vda5050Topics { get; set; } = new();
    public MqttHighAvailabilitySettings MqttHighAvailability { get; set; } = new();
}
