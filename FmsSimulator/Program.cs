using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FmsSimulator.Models;
using FmsSimulator.Services;

// Master control: set to true to run the algorithm tester, false for full sim
bool runTestMode = true;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register implementations for interfaces so the app can be resolved via DI
        services.AddSingleton<IPlanGenerator, PlanGenerator>();
        services.AddSingleton<IMcdmEngine, SimpleMcdmEngine>();
        services.AddSingleton<IDispatcherService, DispatcherService>();
        services.AddSingleton<IJulesMqttClient, JulesMqttClient>();
        services.AddSingleton<ILearningService, LearningService>();
        services.AddSingleton<ErpConnectorService>();
        services.AddSingleton<AlgorithmTester>();
    })
    .Build();

await host.StartAsync();

using var scope = host.Services.CreateScope();
var provider = scope.ServiceProvider;
var logger = provider.GetRequiredService<ILogger<Program>>();

if (runTestMode)
{
    var tester = provider.GetRequiredService<AlgorithmTester>();

    // ARRANGE
    var testFleet = new List<AmrState>
    {
        new() { Id = "Genesis-01", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (10, 5), BatteryLevel = 0.15 },
        new() { Id = "Leviticus-01", ModelName = "Leviticus", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (50, 75), BatteryLevel = 0.95 }
    };

    var testTask = new ProductionTask
    {
        TaskId = "PRIORITY-LIFT",
        RequiredPayload = 1000,
        RequiredModule = "Electric AGV Lift"
    };

    tester.RunTestScenario(
        testName: "Should select farther AMR with higher battery",
        fleet: testFleet,
        task: testTask,
        expectedWinnerId: "Leviticus-01"
    );

    logger.LogInformation("Algorithm test mode completed.");
}
else
{
    var planGenerator = provider.GetRequiredService<IPlanGenerator>();
    var mcdmEngine = provider.GetRequiredService<IMcdmEngine>();
    var dispatcher = provider.GetRequiredService<IDispatcherService>();
    var learningService = provider.GetRequiredService<ILearningService>();
    var erpConnector = provider.GetRequiredService<ErpConnectorService>();

    logger.LogInformation("--- FMS Simulation Initialized (Full Architecture) ---");

    var amrFleet = new List<AmrState>
    {
        new() { Id = "Genesis-01", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (10, 5), BatteryLevel = 0.85 },
        new() { Id = "Exodus-01", ModelName = "Exodus", PrimaryMission = "Mobile Picking", TopModuleType = "6-Axis Robotic Arm", MaxPayloadKg = 250, ArmReachMm = 1200, IsAvailable = true, CurrentPosition = (25, 30), BatteryLevel = 0.75 },
        new() { Id = "Leviticus-01", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (50, 75), BatteryLevel = 0.95 }
    };

    var amrControllers = amrFleet.ToDictionary(amr => amr.Id, amr => new AmrInternalController(amr.Id));

    var taskQueue = erpConnector.FetchAndTranslateOrders();

    int taskNumber = 1;
    while (taskQueue.Count > 0)
    {
        var currentTask = taskQueue.Dequeue();
        logger.LogInformation("Processing Task #{TaskNumber}: {TaskId}", taskNumber, currentTask.TaskId);

        var possiblePlans = planGenerator.GeneratePlans(currentTask, amrFleet);
        var bestPlan = mcdmEngine.SelectBestPlan(possiblePlans);

        if (bestPlan != null)
        {
            bestPlan.AssignedAmr.IsAvailable = false;

            await dispatcher.DispatchOrderAsync(bestPlan);

            var targetController = amrControllers[bestPlan.AssignedAmr.Id];
            await targetController.ProcessVda5050Order(bestPlan.Task);

            double actualTime = bestPlan.PredictedTimeToComplete + System.Random.Shared.Next(5, 10);
            logger.LogInformation("[Feedback] Task complete. Predicted: {Predicted:F2}, Actual: {Actual:F2}", bestPlan.PredictedTimeToComplete, actualTime);
            learningService.UpdateWorldModel(bestPlan, actualTime, planGenerator);
        }
        else
        {
            logger.LogWarning("No available AMR could be found for Task {TaskId}.", currentTask.TaskId);
        }

        taskNumber++;
    }

    logger.LogInformation("All tasks processed. Simulation complete.");
}

await host.StopAsync();

