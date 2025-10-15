using FmsSimulator.Models;
using FmsSimulator.Services;

// --- 1. SETUP ---
var planGenerator = new PlanGenerator();
var mcdmEngine = new SimpleMcdmEngine();
var dispatcher = new DispatcherService(); // NEW: Create an instance of our dispatcher.

Console.WriteLine("--- FMS Simulation Initialized (Full Upgrade) ---");
Console.WriteLine("--------------------------------------------------");

// --- 2. SIMULATE WORLD STATE ---
var amrFleet = new List<AmrState>
{
    new() { Id = "Genesis-01", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (10, 5), BatteryLevel = 0.85 },
    new() { Id = "Exodus-01", ModelName = "Exodus", PrimaryMission = "Mobile Picking", TopModuleType = "6-Axis Robotic Arm", MaxPayloadKg = 250, ArmReachMm = 1200, IsAvailable = true, CurrentPosition = (25, 30), BatteryLevel = 0.75 },
    new() { Id = "Leviticus-01", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (50, 75), BatteryLevel = 0.95 }
};

// --- 3. CREATE A QUEUE OF TASKS ---
var taskQueue = new Queue<ProductionTask>();
taskQueue.Enqueue(new ProductionTask { TaskId = "T-205", RequiredPayload = 1200, RequiredModule = "Electric AGV Lift", RequiredLiftHeight = 500, ToLocation = "Assembly B" });
taskQueue.Enqueue(new ProductionTask { TaskId = "T-206", RequiredPayload = 50, RequiredModule = "6-Axis Robotic Arm", ToLocation = "QC Station" });
taskQueue.Enqueue(new ProductionTask { TaskId = "T-207", RequiredPayload = 900, RequiredModule = "Electric AGV Lift", RequiredLiftHeight = 200, ToLocation = "Warehouse Rack 12" });
taskQueue.Enqueue(new ProductionTask { TaskId = "T-208", RequiredPayload = 1300, RequiredModule = "Electric AGV Lift", RequiredLiftHeight = 400, ToLocation = "Shipping Dock" });

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
        // NEW: Call the dispatcher service to handle communication.
        // The 'await' keyword tells the program to wait for the simulated network call to finish.
        await dispatcher.DispatchOrderAsync(bestPlan); 
    }
    else
    {
        Console.WriteLine($"❌ FMS CORE: No available AMR could be found for Task {currentTask.TaskId}.");
    }
    taskNumber++;
}

Console.WriteLine("\n--------------------------------------------------");
Console.WriteLine("--- All tasks processed. Simulation complete. ---");