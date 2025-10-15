using FmsSimulator.Models;
using FmsSimulator.Services;

// --- 1. SETUP ---
var planGenerator = new PlanGenerator();
var mcdmEngine = new SimpleMcdmEngine();
var dispatcher = new DispatcherService();
var learningService = new LearningService();
var erpConnector = new ErpConnectorService();

Console.WriteLine("--- FMS Simulation Initialized (Full Architecture) ---");
Console.WriteLine("----------------------------------------------------");

// --- 2. SIMULATE WORLD STATE ---
var amrFleet = new List<AmrState>
{
    new() { Id = "Genesis-01", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (10, 5), BatteryLevel = 0.85 },
    new() { Id = "Exodus-01", ModelName = "Exodus", PrimaryMission = "Mobile Picking", TopModuleType = "6-Axis Robotic Arm", MaxPayloadKg = 250, ArmReachMm = 1200, IsAvailable = true, CurrentPosition = (25, 30), BatteryLevel = 0.75 },
    new() { Id = "Leviticus-01", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (50, 75), BatteryLevel = 0.95 }
};

// NEW: Create a "brain" or internal controller for each AMR in our fleet.
var amrControllers = amrFleet.ToDictionary(amr => amr.Id, amr => new AmrInternalController(amr.Id));

// --- 3. VERTICAL COMMUNICATION (ISA-95) ---
var taskQueue = erpConnector.FetchAndTranslateOrders();

// --- 4. MAIN SIMULATION LOOP ---
int taskNumber = 1;
while (taskQueue.Count > 0)
{
    var currentTask = taskQueue.Dequeue();
    Console.WriteLine($"\n--- Processing Task #{taskNumber}: {currentTask.TaskId} ---");
    
    var possiblePlans = planGenerator.GeneratePlans(currentTask, amrFleet);
    var bestPlan = mcdmEngine.SelectBestPlan(possiblePlans);
    
    if (bestPlan != null)
    {
        bestPlan.AssignedAmr.IsAvailable = false;
        
        // --- HORIZONTAL COMMUNICATION (VDA 5050) ---
        await dispatcher.DispatchOrderAsync(bestPlan);

        // --- NEW: INTERNAL COMMUNICATION (Internal MQTT) ---
        // Get the internal controller for the winning AMR and tell it to process the order.
        var targetController = amrControllers[bestPlan.AssignedAmr.Id];
        await targetController.ProcessVda5050Order(bestPlan.Task);

        // --- LEARNING FEEDBACK LOOP ---
        double actualTime = bestPlan.PredictedTimeToComplete + new Random().Next(5, 10); 
        Console.WriteLine($"   [Feedback]: Task complete. Predicted time: {bestPlan.PredictedTimeToComplete:F2}, Actual time: {actualTime:F2}");
        learningService.UpdateWorldModel(bestPlan, actualTime, planGenerator);
    }
    else
    {
        Console.WriteLine($"❌ FMS CORE: No available AMR could be found for Task {currentTask.TaskId}.");
    }
    taskNumber++;
}

Console.WriteLine("\n--------------------------------------------------");
Console.WriteLine("--- All tasks processed. Simulation complete. ---");