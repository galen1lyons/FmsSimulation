using FmsSimulator.Models;
using Microsoft.Extensions.Logging;

namespace FmsSimulator.Services;

public class AlgorithmTester
{
    private readonly IFmsServices.IPlanGenerator _planGenerator;
    private readonly IFmsServices.IMcdmEngine _mcdmEngine;
    private readonly ILogger<AlgorithmTester> _logger;

    public AlgorithmTester(IFmsServices.IPlanGenerator planGenerator, IFmsServices.IMcdmEngine mcdmEngine, ILogger<AlgorithmTester> logger)
    {
        _planGenerator = planGenerator;
        _mcdmEngine = mcdmEngine;
        _logger = logger;
    }

    public void RunTestScenario(string testName, List<AmrState> fleet, ProductionTask task, string expectedWinnerId)
    {
        _logger.LogInformation("--- RUNNING TEST: {TestName} ---", testName);

        // 1. ACT: Run the core decision-making logic.
        var planResult = _planGenerator.GeneratePlans(task, fleet);
        
        if (planResult?.Data is not { } possiblePlans || !possiblePlans.Any())
        {
            _logger.LogWarning("RESULT: FAIL - No valid plans generated.");
            return;
        }

        var decisionResult = _mcdmEngine.SelectBestPlan(possiblePlans);
        
        // 2. ASSERT: Check if the result matches the expectation.
        if (decisionResult?.Data is not { } bestPlan)
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