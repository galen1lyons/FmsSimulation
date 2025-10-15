using FmsSimulator.Models;
using System.Collections.Generic;

namespace FmsSimulator.Services;

public interface IMcdmEngine
{
    AssignmentPlan? SelectBestPlan(List<AssignmentPlan> plans);
}
