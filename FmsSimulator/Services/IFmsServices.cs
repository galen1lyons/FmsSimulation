using FmsSimulator.Models;

namespace FmsSimulator.Services;

public interface IFmsServices
{
    // Plan Generation
    public interface IPlanGenerator
    {
        OperationResult<List<AssignmentPlan>> GeneratePlans(ProductionTask task, IEnumerable<AmrState> fleet);
        void UpdateZoneScore(string zone, double delta);
        void UpdateTrafficCost(string zone, double increase);
    }

    // Decision Making
    public interface IMcdmEngine
    {
        OperationResult<AssignmentPlan> SelectBestPlan(IEnumerable<AssignmentPlan> plans);
    }

    // Communication
    public interface ICommunicationService
    {
        Task PublishVda5050OrderAsync(AssignmentPlan plan);
        Task PublishInternalCommandAsync(string topic, string payload);
        Task ProcessVda5050Order(ProductionTask order);
    }

    // Learning
    public interface ILearningService
    {
        void UpdateWorldModel(AssignmentPlan completedPlan, double actualTimeToComplete, IPlanGenerator planGenerator);
    }

    // ERP Integration
    public interface IErpConnector
    {
        Queue<ProductionTask> FetchAndTranslateOrders();
    }
}