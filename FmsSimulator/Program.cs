using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using FmsSimulator.Services;
using FmsSimulator.Models;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register core services
        services.AddSingleton<LoggingService>();
        services.AddSingleton<IFmsServices.IErpConnector, ErpConnectorService>();
        services.AddSingleton<IFmsServices.IPlanGenerator, OptimizedPlanGenerator>();
        services.AddSingleton<IFmsServices.IMcdmEngine, OptimizedMcdmEngine>();
        services.AddSingleton<IFmsServices.ILearningService, LearningService>();
        services.AddSingleton<IFmsServices.ICommunicationService>(sp => 
            new CommunicationService("SYSTEM"));

        // Validate FMS settings
        services.AddOptions<FmsSettings>()
            .Bind(context.Configuration.GetSection("FmsSettings"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
    })
    .Build();

async Task RunSimulationAsync(IServiceProvider services)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var erpConnector = services.GetRequiredService<IFmsServices.IErpConnector>();
    var planGenerator = services.GetRequiredService<IFmsServices.IPlanGenerator>();
    var mcdmEngine = services.GetRequiredService<IFmsServices.IMcdmEngine>();
    var communicationService = services.GetRequiredService<IFmsServices.ICommunicationService>();
    var learningService = services.GetRequiredService<IFmsServices.ILearningService>();

    // Create a sample fleet of AMRs
    var fleet = new List<AmrState>
    {
        new() {
            Id = "AMR-001",
            ModelName = "HeavyLifter",
            PrimaryMission = "Transport",
            TopModuleType = "Electric AGV Lift",
            MaxPayloadKg = 1500,
            IsAvailable = true,
            CurrentPosition = (1, 1),
            BatteryLevel = 0.95,
            LiftingHeightMm = 1000
        },
        new() {
            Id = "AMR-002",
            ModelName = "RoboArm",
            PrimaryMission = "Pick",
            TopModuleType = "6-Axis Robotic Arm",
            MaxPayloadKg = 100,
            IsAvailable = true,
            CurrentPosition = (2, 3),
            BatteryLevel = 0.75,
            ArmReachMm = 1200
        }
    };

    logger.LogInformation("Starting FMS Simulation with {Count} AMRs in fleet", fleet.Count);

    // Fetch tasks from ERP
    var taskQueue = erpConnector.FetchAndTranslateOrders();
    logger.LogInformation("Received {Count} tasks from ERP", taskQueue.Count);

    // Initialize workflow manager
    var workflowManager = new WorkflowManager(
        erpConnector,
        planGenerator,
        mcdmEngine,
        learningService,
        communicationService);

    // Process tasks concurrently with controlled parallelism
    var maxConcurrentTasks = 3; // Adjust based on system capacity
    var semaphore = new SemaphoreSlim(maxConcurrentTasks);
    var tasks = new List<Task>();

    logger.LogInformation("Starting task processing with max concurrency: {MaxTasks}", maxConcurrentTasks);

    while (taskQueue.Count > 0)
    {
        await semaphore.WaitAsync();
        var currentTask = taskQueue.Dequeue();
        
        var task = Task.Run(async () =>
        {
            try
            {
                await workflowManager.ExecuteWorkflowAsync(currentTask, fleet);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        tasks.Add(task);
    }

    // Wait for all tasks to complete
    await Task.WhenAll(tasks);

    // Generate final statistics
    var completedWorkflows = tasks.Count;
    var failedWorkflows = tasks.Count(t => t.IsFaulted);
    
    logger.LogInformation(
        "Workflow execution completed. Total: {Total}, Succeeded: {Succeeded}, Failed: {Failed}",
        completedWorkflows,
        completedWorkflows - failedWorkflows,
        failedWorkflows);
}

try
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting FMS Simulation...");

    // Register ErpConnectorService which was missing
    var services = host.Services.GetRequiredService<IServiceScopeFactory>()
        .CreateScope().ServiceProvider;

    // Run the simulation
    await RunSimulationAsync(services);

    logger.LogInformation("Services initialized successfully. Press Ctrl+C to shut down.");
    await host.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Critical error starting application: {ex.Message}");
    throw;
}
