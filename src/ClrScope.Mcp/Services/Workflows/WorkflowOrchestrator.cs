using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Workflows;

/// <summary>
/// Orchestrator for automated diagnostic workflows with shared plumbing
/// </summary>
public sealed class WorkflowOrchestrator
{
    private readonly ILogger<WorkflowOrchestrator> _logger;

    public WorkflowOrchestrator(ILogger<WorkflowOrchestrator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Execute a workflow with shared orchestration logic
    /// Note: PID locking is handled by individual collect services, not by the orchestrator
    /// </summary>
    public async Task<WorkflowAutomationResult> ExecuteWorkflowAsync(
        IWorkflow workflow,
        int pid,
        string duration,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();

        try
        {
            _logger.LogInformation("Starting {WorkflowName} for PID {Pid}", workflow.WorkflowName, pid);

            var result = await workflow.ExecuteAsync(pid, duration, cancellationToken);

            artifacts.AddRange(result.Artifacts);
            sessionIds.AddRange(result.SessionIds);

            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "{WorkflowName} completed: {StepsCompleted}/{TotalSteps} steps, {ExecutionTimeMs}ms",
                workflow.WorkflowName,
                result.StepsCompleted,
                workflow.TotalSteps,
                executionTimeMs);

            return new WorkflowAutomationResult(
                Success: result.Success,
                WorkflowName: workflow.WorkflowName,
                StepsCompleted: result.StepsCompleted,
                TotalSteps: workflow.TotalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: result.Error,
                ExecutionTimeMs: executionTimeMs);
        }
        catch (Exception ex)
        {
            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "{WorkflowName} failed for PID {Pid}", workflow.WorkflowName, pid);

            return new WorkflowAutomationResult(
                Success: false,
                WorkflowName: workflow.WorkflowName,
                StepsCompleted: artifacts.Count,
                TotalSteps: workflow.TotalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: ex.Message,
                ExecutionTimeMs: executionTimeMs);
        }
    }
}
