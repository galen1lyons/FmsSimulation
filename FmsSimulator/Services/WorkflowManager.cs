using FmsSimulator.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FmsSimulator.Services;

public class WorkflowManager
{
    private readonly IFmsServices.IErpConnector _erpConnector;
    private readonly IFmsServices.IPlanGenerator _planGenerator;
    private readonly IFmsServices.IMcdmEngine _mcdmEngine;
    private readonly IFmsServices.ILearningService _learningService;
    private readonly IFmsServices.ICommunicationService _communicationService;
    private readonly LoggingService _logger = LoggingService.Instance;
    private readonly ConcurrentDictionary<string, WorkflowState> _activeWorkflows = new();

    public record WorkflowState
    {
        public string TaskId { get; init; } = "";
        public string Status { get; set; } = "";
        public DateTime StartTime { get; init; }
        public DateTime? CompletionTime { get; set; }
        public List<string> StateTransitions { get; } = new();
        public Dictionary<string, double> Metrics { get; } = new();
    }

    public WorkflowManager(
        IFmsServices.IErpConnector erpConnector,
        IFmsServices.IPlanGenerator planGenerator,
        IFmsServices.IMcdmEngine mcdmEngine,
        IFmsServices.ILearningService learningService,
        IFmsServices.ICommunicationService communicationService)
    {
        _erpConnector = erpConnector ?? throw new ArgumentNullException(nameof(erpConnector));
        _planGenerator = planGenerator ?? throw new ArgumentNullException(nameof(planGenerator));
        _mcdmEngine = mcdmEngine ?? throw new ArgumentNullException(nameof(mcdmEngine));
        _learningService = learningService ?? throw new ArgumentNullException(nameof(learningService));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
    }

    public async Task ExecuteWorkflowAsync(ProductionTask task, IEnumerable<AmrState> fleet)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(fleet);

        var workflowState = new WorkflowState 
        { 
            TaskId = task.TaskId,
            StartTime = DateTime.UtcNow,
            Status = "STARTED"
        };
        
        if (!_activeWorkflows.TryAdd(task.TaskId, workflowState))
        {
            throw new InvalidOperationException($"Workflow for task {task.TaskId} already exists");
        }

        try
        {
            // Phase 1: Plan Generation
            UpdateWorkflowState(task.TaskId, "PLANNING", "Generating plans");
            var planResult = await Task.Run(() => _planGenerator.GeneratePlans(task, fleet));
            
            if (planResult?.Data is not { } validPlans || !validPlans.Any())
            {
                throw new InvalidOperationException("No valid plans could be generated");
            }

            // Track planning metrics
            if (planResult.Metrics is { } metrics)
            {
                foreach (var (key, value) in metrics)
                {
                    workflowState.Metrics[$"planning_{key}"] = Convert.ToDouble(value);
                }
            }

            // Phase 2: Decision Making
            UpdateWorkflowState(task.TaskId, "PLANNING", "Selecting optimal plan");
            var decisionResult = await Task.Run(() => _mcdmEngine.SelectBestPlan(validPlans));
            
            if (decisionResult?.Data is not { } selectedPlan)
            {
                throw new InvalidOperationException("Could not select optimal plan");
            }

            // Track decision metrics
            if (decisionResult.Metrics is { } decisionMetrics)
            {
                foreach (var (key, value) in decisionMetrics)
                {
                    workflowState.Metrics[$"decision_{key}"] = Convert.ToDouble(value);
                }
            }

            // Phase 3: Execution
            UpdateWorkflowState(task.TaskId, "EXECUTING", "Publishing order");
            await _communicationService.PublishVda5050OrderAsync(selectedPlan);
            
            UpdateWorkflowState(task.TaskId, "EXECUTING", "Processing order");
            await _communicationService.ProcessVda5050Order(task);

            // Phase 4: Learning & Feedback
            UpdateWorkflowState(task.TaskId, "LEARNING", "Updating world model");
            var actualTime = selectedPlan.PredictedTimeToComplete * 1.1; // Simulate 10% variance
            _learningService.UpdateWorldModel(selectedPlan, actualTime, _planGenerator);

            workflowState.CompletionTime = DateTime.UtcNow;
            workflowState.Metrics["actual_completion_time"] = actualTime;
            workflowState.Metrics["prediction_accuracy"] = 
                Math.Abs(1 - (actualTime / selectedPlan.PredictedTimeToComplete));

            // Log final metrics
            _logger.LogOperationalMetrics("WorkflowManager", "WorkflowComplete", 
                new Dictionary<string, object>
                {
                    ["taskId"] = task.TaskId,
                    ["duration"] = (workflowState.CompletionTime - workflowState.StartTime).Value.TotalSeconds,
                    ["metrics"] = workflowState.Metrics
                });

            UpdateWorkflowState(task.TaskId, "COMPLETED", "Task completed successfully");
        }
        catch (Exception ex)
        {
            UpdateWorkflowState(task.TaskId, "FAILED", ex.Message);
            _logger.LogError("WorkflowManager", $"Workflow_{task.TaskId}", ex);
            throw;
        }
    }

    private void UpdateWorkflowState(string taskId, string newStatus, string? details = null)
    {
        if (_activeWorkflows.TryGetValue(taskId, out var state))
        {
            state.Status = newStatus;
            state.StateTransitions.Add($"{DateTime.UtcNow:HH:mm:ss.fff} -> {newStatus}{(details != null ? $": {details}" : "")}");
            
            _logger.LogOperationalMetrics("WorkflowManager", "StateTransition", new Dictionary<string, object>
            {
                ["taskId"] = taskId,
                ["newStatus"] = newStatus,
                ["details"] = details ?? "",
                ["transitionCount"] = state.StateTransitions.Count
            });
        }
    }

    public WorkflowState? GetWorkflowState(string taskId) => 
        _activeWorkflows.TryGetValue(taskId, out var state) ? state : null;

    public IEnumerable<WorkflowState> GetActiveWorkflows() => 
        _activeWorkflows.Values.Where(w => w.Status != "COMPLETED" && w.Status != "FAILED");
}