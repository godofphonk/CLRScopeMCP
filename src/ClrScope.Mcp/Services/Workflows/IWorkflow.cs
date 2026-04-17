using ClrScope.Mcp.Services.Collect;

namespace ClrScope.Mcp.Services.Workflows;

/// <summary>
/// Interface for automated diagnostic workflows
/// </summary>
public interface IWorkflow
{
    /// <summary>
    /// Workflow name
    /// </summary>
    string WorkflowName { get; }

    /// <summary>
    /// Total number of steps in the workflow
    /// </summary>
    int TotalSteps { get; }

    /// <summary>
    /// Execute the workflow
    /// </summary>
    Task<WorkflowAutomationResult> ExecuteAsync(
        int pid,
        string duration,
        CancellationToken cancellationToken);
}
