using FmsSimulator.Models;
using FmsSimulator.Services;

// --- 1. SETUP ---
// Create instances of our services.
var planGenerator = new PlanGenerator();
var mcdmEngine = new SimpleMcdmEngine();

Console.WriteLine("--- FMS Simulation Initialized ---");
Console.WriteLine("----------------------------------");

/// --- 2. SIMULATE WORLD STATE ---
// Define the fleet of AMRs based on the specs you provided.
var amrFleet = new List<AmrState>
{
    new() { Id = "Genesis-01", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (10, 5) },
    new() { Id = "Genesis-02", ModelName = "Genesis", PrimaryMission = "Pallet & Rack Transport", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 1500, LiftingHeightMm = 510, IsAvailable = true, CurrentPosition = (50, 75) },
    new() { Id = "Exodus-01", ModelName = "Exodus", PrimaryMission = "Mobile Picking", TopModuleType = "6-Axis Robotic Arm", MaxPayloadKg = 250, ArmReachMm = 1200, IsAvailable = true, CurrentPosition = (25, 30) }
};

// --- 3. A NEW TASK ARRIVES ---
// Simulate a new job order coming from the ERP/MES.
var newTask = new ProductionTask
{
    TaskId = "T-205",
    FromLocation = "Dock A",
    ToLocation = "Assembly B",
    RequiredPayload = 1200, // A heavy pallet
    RequiredModule = "Electric AGV Lift",
    RequiredLiftHeight = 500
};

Console.WriteLine($"\nNew Task Received: {newTask.TaskId} (Requires heavy lift)");
Console.WriteLine("----------------------------------");

// --- 4. RUN THE DECISION LOGIC ---
// Use the "subconscious brain" to find all possible solutions.
var possiblePlans = planGenerator.GeneratePlans(newTask, amrFleet);

// Use the "conscious brain" to score the possibilities and pick the best one.
var bestPlan = mcdmEngine.SelectBestPlan(possiblePlans);

// --- 5. SHOW THE FINAL DECISION ---
Console.WriteLine("----------------------------------");
if (bestPlan != null)
{
    Console.WriteLine($"✅ FINAL DECISION: Assign Task {bestPlan.Task.TaskId} to AMR {bestPlan.AssignedAmr.Id}.");
}
else
{
    Console.WriteLine("❌ FINAL DECISION: No suitable AMR could be found for the task.");
}
Console.WriteLine("----------------------------------");