using FmsSimulator.Models;

namespace FmsSimulator.Services;

public interface ILearningService
{
    void UpdateWorldModel(AssignmentPlan completedPlan, double actualTimeToComplete, IPlanGenerator planGenerator);
}
