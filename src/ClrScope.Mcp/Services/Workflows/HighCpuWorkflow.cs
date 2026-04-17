using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Collect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Services.Workflows;

/// <summary>
/// Automated high CPU diagnostic workflow - collects trace, counters, and stacks
/// </summary>
public sealed class HighCpuWorkflow : IWorkflow
{
    private readonly ILogger<HighCpuWorkflow> _logger;
    private readonly CollectTraceService _traceService;
    private readonly CollectCountersService _countersService;
    private readonly CollectStacksService _stacksService;
    private readonly IOptions<ClrScopeOptions> _options;

    public HighCpuWorkflow(
        ILogger<HighCpuWorkflow> logger,
        CollectTraceService traceService,
        CollectCountersService countersService,
        CollectStacksService stacksService,
        IOptions<ClrScopeOptions> options)
    {
        _logger = logger;
        _traceService = traceService;
        _countersService = countersService;
        _stacksService = stacksService;
        _options = options;
    }

    public string WorkflowName => "automated_high_cpu_bundle";
    public int TotalSteps => 3;

    public async Task<WorkflowAutomationResult> ExecuteAsync(
        int pid,
        string duration,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();
        int stepsCompleted = 0;

        // Step 1: Collect trace
        _logger.LogInformation("Step 1/3: Collecting trace for PID {Pid}", pid);
        var traceRequest = new CollectTraceRequest(pid, duration, Profile: "cpu-sampling");
        var traceResult = await _traceService.CollectTraceAsync(traceRequest, null, cancellationToken);
        if (traceResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(traceResult.Artifact.ArtifactId.Value, "trace", traceResult.Artifact.FilePath, traceResult.Artifact.SizeBytes));
            sessionIds.Add(traceResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 1/3 completed: Trace collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 1/3 failed: Trace collection failed - {Error}", traceResult.Error);
        }

        // Step 2: Collect counters
        _logger.LogInformation("Step 2/3: Collecting counters for PID {Pid}", pid);
        var countersRequest = new CollectCountersRequest(pid, duration, Providers: _options.Value.DefaultCountersProviders);
        var countersResult = await _countersService.CollectCountersAsync(countersRequest, null, cancellationToken);
        if (countersResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(countersResult.Artifact.ArtifactId.Value, "counters", countersResult.Artifact.FilePath, countersResult.Artifact.SizeBytes));
            sessionIds.Add(countersResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 2/3 completed: Counters collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 2/3 failed: Counters collection failed - {Error}", countersResult.Error);
        }

        // Step 3: Collect stacks
        _logger.LogInformation("Step 3/3: Collecting stacks for PID {Pid}", pid);
        var stacksRequest = new CollectStacksRequest(pid);
        var stacksResult = await _stacksService.CollectStacksAsync(stacksRequest, null, cancellationToken);
        if (stacksResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(stacksResult.Artifact.ArtifactId.Value, "stacks", stacksResult.Artifact.FilePath, stacksResult.Artifact.SizeBytes));
            sessionIds.Add(stacksResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 3/3 completed: Stacks collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 3/3 failed: Stacks collection failed - {Error}", stacksResult.Error);
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
            Error: error);
    }
}
