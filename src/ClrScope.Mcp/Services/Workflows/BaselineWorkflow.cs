using ClrScope.Mcp.Services.Collect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ClrScope.Mcp.Tools.Workflows;

namespace ClrScope.Mcp.Services.Workflows;

/// <summary>
/// Automated baseline performance workflow - collects counters, trace, gcdump, and stacks
/// </summary>
public sealed class BaselineWorkflow : IWorkflow
{
    private readonly ILogger<BaselineWorkflow> _logger;

    public BaselineWorkflow(ILogger<BaselineWorkflow> logger)
    {
        _logger = logger;
    }

    public string WorkflowName => "automated_baseline_bundle";
    public int TotalSteps => 4;

    public async Task<WorkflowAutomationResult> ExecuteAsync(
        int pid,
        IServiceProvider serviceProvider,
        string duration,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();
        int stepsCompleted = 0;

        // Step 1: Collect performance counters
        _logger.LogInformation("Step 1/4: Collecting performance counters for PID {Pid}", pid);
        var countersService = serviceProvider.GetRequiredService<CollectCountersService>();
        var countersRequest = new CollectCountersRequest(pid, duration, Providers: new[] { "System.Runtime" });
        var countersResult = await countersService.CollectCountersAsync(countersRequest, null, cancellationToken);
        if (countersResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(countersResult.Artifact.ArtifactId.Value, "counters", countersResult.Artifact.FilePath, countersResult.Artifact.SizeBytes));
            sessionIds.Add(countersResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 1/4 completed: Performance counters collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 1/4 failed: Performance counters collection failed - {Error}", countersResult.Error);
        }

        // Step 2: Collect baseline trace
        _logger.LogInformation("Step 2/4: Collecting baseline trace for PID {Pid}", pid);
        var traceService = serviceProvider.GetRequiredService<CollectTraceService>();
        var traceRequest = new CollectTraceRequest(pid, duration, Profile: "cpu-sampling");
        var traceResult = await traceService.CollectTraceAsync(traceRequest, null, cancellationToken);
        if (traceResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(traceResult.Artifact.ArtifactId.Value, "trace", traceResult.Artifact.FilePath, traceResult.Artifact.SizeBytes));
            sessionIds.Add(traceResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 2/4 completed: Baseline trace collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 2/4 failed: Baseline trace collection failed - {Error}", traceResult.Error);
        }

        // Step 3: Collect GC dump
        _logger.LogInformation("Step 3/4: Collecting GC dump for PID {Pid}", pid);
        var gcdumpService = serviceProvider.GetRequiredService<CollectGcDumpService>();
        var gcdumpRequest = new CollectGcDumpRequest(pid);
        var gcdumpResult = await gcdumpService.CollectGcDumpAsync(gcdumpRequest, null, cancellationToken);
        if (gcdumpResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(gcdumpResult.Artifact.ArtifactId.Value, "gcdump", gcdumpResult.Artifact.FilePath, gcdumpResult.Artifact.SizeBytes));
            sessionIds.Add(gcdumpResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 3/4 completed: GC dump collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 3/4 failed: GC dump collection failed - {Error}", gcdumpResult.Error);
        }

        // Step 4: Collect thread stacks
        _logger.LogInformation("Step 4/4: Collecting thread stacks for PID {Pid}", pid);
        var stacksService = serviceProvider.GetRequiredService<CollectStacksService>();
        var stacksRequest = new CollectStacksRequest(pid);
        var stacksResult = await stacksService.CollectStacksAsync(stacksRequest, null, cancellationToken);
        if (stacksResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(stacksResult.Artifact.ArtifactId.Value, "stacks", stacksResult.Artifact.FilePath, stacksResult.Artifact.SizeBytes));
            sessionIds.Add(stacksResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 4/4 completed: Thread stacks collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 4/4 failed: Thread stacks collection failed - {Error}", stacksResult.Error);
        }

        var success = stepsCompleted == TotalSteps;
        var error = success ? null : $"Completed {stepsCompleted}/{TotalSteps} steps";

        return new WorkflowAutomationResult(
            Success: success,
            WorkflowName: WorkflowName,
            StepsCompleted: stepsCompleted,
            TotalSteps: TotalSteps,
            Artifacts: artifacts.ToArray(),
            SessionIds: sessionIds.ToArray(),
            Error: error,
            ExecutionTimeMs: 0);
    }
}
