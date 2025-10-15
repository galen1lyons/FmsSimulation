using FmsSimulator.Models;
using System.Collections.Generic;

namespace FmsSimulator.Services;

public interface IPlanGenerator
{
    List<AssignmentPlan> GeneratePlans(ProductionTask task, List<AmrState> fleet);
    void UpdateTrafficCost(string zone, double increase);
}
