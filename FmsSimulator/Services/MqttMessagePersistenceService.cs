using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FmsSimulator.Models;

namespace FmsSimulator.Services
{
    /// <summary>
    /// MQTT Message Persistence Service - Persists messages to disk for reliability.
    /// 
    /// Key Responsibilities:
    /// - Persist pending MQTT messages to disk
    /// - Load persisted messages on startup
    /// - Automatic cleanup of old messages
    /// - Message expiration handling
    /// - Thread-safe file operations
    /// </summary>
    public class MqttMessagePersistenceService : IDisposable
    {
        private readonly MqttHighAvailabilitySettings _settings;
        private readonly ILogger<MqttMessagePersistenceService> _logger;
        private readonly LoggingService _structuredLogger = LoggingService.Instance;

        private readonly string _persistenceDirectory;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly TimeSpan _messageExpiration = TimeSpan.FromHours(24);

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public MqttMessagePersistenceService(
            IOptions<MqttSettings> mqttSettings,
            ILogger<MqttMessagePersistenceService> logger)
        {
            _settings = mqttSettings.Value?.MqttHighAvailability ?? throw new ArgumentNullException(nameof(mqttSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _persistenceDirectory = Path.GetFullPath(_settings.PersistenceDirectory);

            // Ensure directory exists
            if (_settings.EnableMessagePersistence)
            {
                Directory.CreateDirectory(_persistenceDirectory);
                _logger.LogInformation("Message persistence enabled. Directory: {Directory}", _persistenceDirectory);
            }
        }

        /// <summary>
        /// Persists a batch of messages to disk.
        /// </summary>
        public async Task PersistMessagesAsync(List<PersistedMessage> messages)
        {
            if (!_settings.EnableMessagePersistence || messages.Count == 0)
                return;

            await _fileLock.WaitAsync();
            try
            {
                string fileName = $"pending_messages_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
                string filePath = Path.Combine(_persistenceDirectory, fileName);

                var batch = new PersistedMessageBatch
                {
                    CreatedAt = DateTime.UtcNow,
                    Messages = messages
                };

                string json = JsonSerializer.Serialize(batch, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogInformation(
                    "Persisted {Count} messages to disk. File: {FileName}",
                    messages.Count, fileName);

                _structuredLogger.LogOperationalMetrics("MqttPersistence", "MessagesPersisted", new Dictionary<string, object>
                {
                    ["messageCount"] = messages.Count,
                    ["fileName"] = fileName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist messages to disk.");
                _structuredLogger.LogError("MqttPersistence", "PersistFailed", ex);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Loads all persisted messages from disk.
        /// </summary>
        public async Task<List<PersistedMessage>> LoadPersistedMessagesAsync()
        {
            if (!_settings.EnableMessagePersistence)
                return new List<PersistedMessage>();

            var allMessages = new List<PersistedMessage>();

            await _fileLock.WaitAsync();
            try
            {
                if (!Directory.Exists(_persistenceDirectory))
                    return allMessages;

                var files = Directory.GetFiles(_persistenceDirectory, "pending_messages_*.json");

                _logger.LogInformation("Found {Count} persisted message files.", files.Length);

                foreach (var filePath in files)
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(filePath);
                        var batch = JsonSerializer.Deserialize<PersistedMessageBatch>(json, _jsonOptions);

                        if (batch != null && batch.Messages != null)
                        {
                            // Filter out expired messages
                            var validMessages = batch.Messages
                                .Where(m => DateTime.UtcNow - m.Timestamp < _messageExpiration)
                                .ToList();

                            allMessages.AddRange(validMessages);

                            _logger.LogInformation(
                                "Loaded {ValidCount}/{TotalCount} messages from {FileName}",
                                validMessages.Count, batch.Messages.Count, Path.GetFileName(filePath));
                        }

                        // Delete file after loading
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load messages from file: {FilePath}", filePath);
                    }
                }

                if (allMessages.Count > 0)
                {
                    _logger.LogInformation(
                        "Total persisted messages loaded: {Count}",
                        allMessages.Count);

                    _structuredLogger.LogOperationalMetrics("MqttPersistence", "MessagesLoaded", new Dictionary<string, object>
                    {
                        ["messageCount"] = allMessages.Count,
                        ["fileCount"] = files.Length
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load persisted messages.");
            }
            finally
            {
                _fileLock.Release();
            }

            return allMessages;
        }

        /// <summary>
        /// Cleans up old persisted message files.
        /// </summary>
        public async Task CleanupOldFilesAsync()
        {
            if (!_settings.EnableMessagePersistence)
                return;

            await _fileLock.WaitAsync();
            try
            {
                if (!Directory.Exists(_persistenceDirectory))
                    return;

                var files = Directory.GetFiles(_persistenceDirectory, "pending_messages_*.json");
                var cutoffTime = DateTime.UtcNow - _messageExpiration;

                int deletedCount = 0;

                foreach (var filePath in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.CreationTimeUtc < cutoffTime)
                        {
                            File.Delete(filePath);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old file: {FilePath}", filePath);
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} old persisted message files.", deletedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old persisted message files.");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Gets statistics about persisted messages.
        /// </summary>
        public async Task<PersistenceStatistics> GetStatisticsAsync()
        {
            var stats = new PersistenceStatistics
            {
                IsEnabled = _settings.EnableMessagePersistence,
                PersistenceDirectory = _persistenceDirectory
            };

            if (!_settings.EnableMessagePersistence)
                return stats;

            await _fileLock.WaitAsync();
            try
            {
                if (Directory.Exists(_persistenceDirectory))
                {
                    var files = Directory.GetFiles(_persistenceDirectory, "pending_messages_*.json");
                    stats.FileCount = files.Length;

                    long totalSize = 0;
                    foreach (var file in files)
                    {
                        totalSize += new FileInfo(file).Length;
                    }
                    stats.TotalSizeBytes = totalSize;
                }
            }
            finally
            {
                _fileLock.Release();
            }

            return stats;
        }

        public void Dispose()
        {
            _fileLock?.Dispose();
        }
    }

    // ==================================================================================
    // MODELS
    // ==================================================================================

    /// <summary>
    /// Represents a persisted MQTT message.
    /// </summary>
    public class PersistedMessage
    {
        public string Topic { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public int QoS { get; set; } = 1;
        public bool Retain { get; set; }
        public DateTime Timestamp { get; set; }
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Batch of persisted messages saved together.
    /// </summary>
    public class PersistedMessageBatch
    {
        public DateTime CreatedAt { get; set; }
        public List<PersistedMessage> Messages { get; set; } = new();
    }

    /// <summary>
    /// Statistics about message persistence.
    /// </summary>
    public class PersistenceStatistics
    {
        public bool IsEnabled { get; set; }
        public string PersistenceDirectory { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public long TotalSizeBytes { get; set; }

        public double TotalSizeMB => TotalSizeBytes / 1024.0 / 1024.0;
    }
}
