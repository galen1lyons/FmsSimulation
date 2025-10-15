using FmsSimulator.Models;
using Microsoft.Extensions.Logging;

namespace FmsSimulator.Services;

public class AlgorithmTester
{
    private readonly PlanGenerator _planGenerator;
    private readonly SimpleMcdmEngine _mcdmEngine;
    private readonly ILogger<AlgorithmTester> _logger;

    public AlgorithmTester(PlanGenerator planGenerator, SimpleMcdmEngine mcdmEngine, ILogger<AlgorithmTester> logger)
    {
        _planGenerator = planGenerator;
        _mcdmEngine = mcdmEngine;
        _logger = logger;
    }

    public void RunTestScenario(string testName, List<AmrState> fleet, ProductionTask task, string expectedWinnerId)
    {
        _logger.LogInformation("--- RUNNING TEST: {TestName} ---", testName);

        // 1. ACT: Run the core decision-making logic.
        var possiblePlans = _planGenerator.GeneratePlans(task, fleet);
        var bestPlan = _mcdmEngine.SelectBestPlan(possiblePlans);

        // 2. ASSERT: Check if the result matches the expectation.
        if (bestPlan == null)
        {
            _logger.LogWarning("RESULT: FAIL - The algorithm did not select any AMR.");
            return;
        }

        if (bestPlan.AssignedAmr.Id == expectedWinnerId)
        {
            _logger.LogInformation("✅ RESULT: PASS - The algorithm correctly selected {Expected}.", expectedWinnerId);
        }
        else
        {
            _logger.LogError("❌ RESULT: FAIL - Expected {Expected} but the algorithm chose {Actual}.", expectedWinnerId, bestPlan.AssignedAmr.Id);
        }
        _logger.LogInformation("------------------------------------------");
    }
}