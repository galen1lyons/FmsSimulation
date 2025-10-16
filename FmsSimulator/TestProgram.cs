using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FmsSimulator.Models;
using FmsSimulator.Services;
using FmsSimulator.Services.MQTT;

namespace FmsSimulator;

/// <summary>
/// Test program for Phase 1 MQTT integration testing.
/// Configures services and runs comprehensive validation tests.
/// </summary>
public class TestProgram
{
    /// <summary>
    /// Runs the MQTT integration test harness.
    /// </summary>
    /// <param name="args">Command line arguments: [brokerHost] [brokerPort]</param>
    public static async Task RunAsync(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        FMS MQTT INTEGRATION TEST HARNESS - Phase 1            ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Check for broker host argument
        string brokerHost = args.Length > 0 ? args[0] : "test.mosquitto.org";
        int brokerPort = args.Length > 1 ? int.Parse(args[1]) : 1883;

        Console.WriteLine($"📡 Broker: {brokerHost}:{brokerPort}");
        Console.WriteLine($"   (Use public test broker or specify: dotnet run -- localhost 1883)");
        Console.WriteLine();

        // Option to use local broker if available
        if (brokerHost == "test.mosquitto.org")
        {
            Console.WriteLine("⚠️  Using public test broker: test.mosquitto.org");
            Console.WriteLine("   This is shared infrastructure - tests may interfere with others.");
            Console.WriteLine("   For production testing, deploy a local MQTT broker:");
            Console.WriteLine("   - Docker: docker run -d -p 1883:1883 eclipse-mosquitto:2.0");
            Console.WriteLine("   - Windows: Download from https://mosquitto.org/download/");
            Console.WriteLine();
            
            Console.Write("Continue with public broker? (y/n): ");
            var response = Console.ReadLine()?.ToLower();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Test cancelled. Deploy a local broker and rerun with: dotnet run -- localhost 1883");
                return;
            }
            Console.WriteLine();
        }

        try
        {
            // Build service provider
            var serviceProvider = BuildServiceProvider(brokerHost, brokerPort);

            // Get services
            var testHarness = serviceProvider.GetRequiredService<MqttIntegrationTestHarness>();
            var haOrchestrator = serviceProvider.GetRequiredService<MqttHighAvailabilityOrchestrator>();

            // Start HA orchestrator
            Console.WriteLine("🚀 Starting High-Availability Orchestrator...");
            await haOrchestrator.StartAsync();
            Console.WriteLine("✅ HA Orchestrator started");
            Console.WriteLine();

            // Wait for initial connection
            await Task.Delay(2000);

            // Run all tests
            Console.WriteLine("🧪 Running integration tests...");
            Console.WriteLine(new string('═', 80));
            Console.WriteLine();

            var summary = await testHarness.RunAllTestsAsync();

            // Stop HA orchestrator
            Console.WriteLine();
            Console.WriteLine("🛑 Stopping High-Availability Orchestrator...");
            await haOrchestrator.StopAsync();
            Console.WriteLine("✅ HA Orchestrator stopped");
            Console.WriteLine();

            // Print summary
            Console.WriteLine(new string('═', 80));
            Console.WriteLine("                         FINAL TEST SUMMARY");
            Console.WriteLine(new string('═', 80));
            Console.WriteLine($"Total Tests:   {summary.TotalTests}");
            Console.WriteLine($"Passed:        {summary.TestsPassed} ✅");
            Console.WriteLine($"Failed:        {summary.TestsFailed} ❌");
            Console.WriteLine($"Success Rate:  {(summary.TotalTests > 0 ? (double)summary.TestsPassed / summary.TotalTests * 100 : 0):F1}%");
            Console.WriteLine($"Duration:      {summary.Duration.TotalSeconds:F2}s");
            Console.WriteLine(new string('═', 80));
            Console.WriteLine();

            // Exit code
            int exitCode = summary.TestsFailed == 0 ? 0 : 1;
            
            if (exitCode == 0)
            {
                Console.WriteLine("🎉 All tests passed! Phase 1 is ready for production.");
            }
            else
            {
                Console.WriteLine($"⚠️  {summary.TestsFailed} test(s) failed. Review logs above.");
            }

            Environment.Exit(exitCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("❌ FATAL ERROR:");
            Console.WriteLine(ex.Message);
            Console.WriteLine();
            Console.WriteLine("Stack Trace:");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    private static IServiceProvider BuildServiceProvider(string brokerHost, int brokerPort)
    {
        var services = new ServiceCollection();

        // Logging configuration
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("System", LogLevel.Warning);
            builder.AddFilter("FmsSimulator", LogLevel.Information);
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // MQTT Broker Settings
        services.Configure<MqttBrokerSettings>(options =>
        {
            options.BrokerHost = brokerHost;
            options.BrokerPort = brokerPort;
            options.ClientId = $"FmsSimulator_{Guid.NewGuid():N}";
            options.Username = null;
            options.Password = null;
            options.UseTls = false;
            options.AutoReconnect = true;
            options.KeepAliveSeconds = 60;
            options.DefaultQoS = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce;
        });

        // VDA 5050 Topic Settings
        services.Configure<Vda5050TopicSettings>(options =>
        {
            options.BaseTopicPrefix = "vda5050/v2";
            options.Manufacturer = "FMS";
            options.SerialNumber = Environment.MachineName;
        });

        // High-Availability Settings
        services.Configure<MqttHighAvailabilitySettings>(options =>
        {
            options.EnableCircuitBreaker = true;
            options.CircuitBreakerThreshold = 5;
            options.CircuitBreakerTimeoutSeconds = 60;
            options.EnableMessagePersistence = true;
            options.PersistenceDirectory = "./mqtt_persistence";
            options.MaxPersistedMessages = 10000;
        });

        // Combined MQTT Settings wrapper
        services.Configure<MqttSettings>(options =>
        {
            options.MqttBroker = new MqttBrokerSettings
            {
                BrokerHost = brokerHost,
                BrokerPort = brokerPort,
                ClientId = $"FmsSimulator_{Guid.NewGuid():N}",
                AutoReconnect = true,
                KeepAliveSeconds = 60,
                DefaultQoS = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
            };

            options.Vda5050Topics = new Vda5050TopicSettings
            {
                BaseTopicPrefix = "vda5050/v2",
                Manufacturer = "FMS",
                SerialNumber = Environment.MachineName
            };

            options.MqttHighAvailability = new MqttHighAvailabilitySettings
            {
                EnableCircuitBreaker = true,
                CircuitBreakerThreshold = 5,
                CircuitBreakerTimeoutSeconds = 60,
                EnableMessagePersistence = true,
                PersistenceDirectory = "./mqtt_persistence",
                MaxPersistedMessages = 10000
            };
        });

        // Register MQTT services
        services.AddSingleton<MqttClientService>();
        services.AddSingleton<MqttHealthMonitor>();
        services.AddSingleton<MqttMessagePersistenceService>();
        services.AddSingleton<MqttHighAvailabilityOrchestrator>();
        services.AddSingleton<Vda5050PublisherService>();
        services.AddSingleton<Vda5050SubscriberService>();

        // Register test harness
        services.AddSingleton<MqttIntegrationTestHarness>();

        return services.BuildServiceProvider();
    }
}
