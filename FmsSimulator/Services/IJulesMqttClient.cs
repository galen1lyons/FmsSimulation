using FmsSimulator.Models;
using System.Threading.Tasks;

namespace FmsSimulator.Services;

public interface IJulesMqttClient
{
    Task PublishAsync(AssignmentPlan winningPlan);
}
