using FmsSimulator.Models;
using System.Threading.Tasks;

namespace FmsSimulator.Services;

public interface IDispatcherService
{
    Task DispatchOrderAsync(AssignmentPlan winningPlan);
}
