using FmsSimulator.Models;
using Microsoft.Extensions.Logging;

namespace FmsSimulator.Services;

public class LearningService : IFmsServices.ILearningService
{
    private readonly ILogger<LearningService> _logger;

    public LearningService(ILogger<LearningService> logger)
    {
        _logger = logger;
    }
    // This method represents the "Act" phase of the PDCA cycle.
    public void UpdateWorldModel(AssignmentPlan completedPlan, double actualTimeToComplete, IFmsServices.IPlanGenerator planGenerator)
    {
        // "CHECK": Compare the prediction with the actual result.
        double error = actualTimeToComplete - completedPlan.PredictedTimeToComplete;

        // If the task took significantly longer than predicted...
        if (error > 2.0) // If it was more than 2 time units slower
        {
            _logger.LogInformation("[Learning Service]: Task {TaskId} was slower than predicted. Analyzing path...", completedPlan.Task.TaskId);

            // "ACT": Update the internal model.
            // In a real system, we'd analyze the path. Here, we'll just increase the cost
            // for the zone the robot started in as a simple simulation of learning.
            string zoneToUpdate = $"Zone_{completedPlan.AssignedAmr.CurrentPosition.X}_{completedPlan.AssignedAmr.CurrentPosition.Y}";

            planGenerator.UpdateTrafficCost(zoneToUpdate, 0.1); // Increase cost by 10%
        }
    }
}