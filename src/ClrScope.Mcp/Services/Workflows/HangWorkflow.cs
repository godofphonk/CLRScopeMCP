using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Collect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Services.Workflows;

/// <summary>
/// Automated hang/deadlock diagnostic workflow - collects dump, stacks, and counters
/// </summary>
public sealed class HangWorkflow : IWorkflow
{
    private readonly ILogger<HangWorkflow> _logger;
    private readonly CollectDumpService _dumpService;
    private readonly CollectStacksService _stacksService;
    private readonly CollectCountersService _countersService;
    private readonly IOptions<ClrScopeOptions> _options;

    public HangWorkflow(
        ILogger<HangWorkflow> logger,
        CollectDumpService dumpService,
        CollectStacksService stacksService,
        CollectCountersService countersService,
        IOptions<ClrScopeOptions> options)
    {
        _logger = logger;
        _dumpService = dumpService;
        _stacksService = stacksService;
        _countersService = countersService;
        _options = options;
    }

    public string WorkflowName => "automated_hang_bundle";
    public int TotalSteps => 3;

    public async Task<WorkflowAutomationResult> ExecuteAsync(
        int pid,
        IServiceProvider serviceProvider,
        string duration,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();
        int stepsCompleted = 0;

        // Step 1: Collect memory dump
        _logger.LogInformation("Step 1/3: Collecting memory dump for PID {Pid}", pid);
        var dumpRequest = new CollectDumpRequest(pid, IncludeHeap: true, Compress: false);
        var dumpResult = await _dumpService.CollectDumpAsync(dumpRequest, null, cancellationToken);
        if (dumpResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(dumpResult.Artifact.ArtifactId.Value, "dump", dumpResult.Artifact.FilePath, dumpResult.Artifact.SizeBytes));
            sessionIds.Add(dumpResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 1/3 completed: Memory dump collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 1/3 failed: Memory dump collection failed - {Error}", dumpResult.Error);
        }

        // Step 2: Collect thread stacks
        _logger.LogInformation("Step 2/3: Collecting thread stacks for PID {Pid}", pid);
        var stacksRequest = new CollectStacksRequest(pid);
        var stacksResult = await _stacksService.CollectStacksAsync(stacksRequest, null, cancellationToken);
        if (stacksResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(stacksResult.Artifact.ArtifactId.Value, "stacks", stacksResult.Artifact.FilePath, stacksResult.Artifact.SizeBytes));
            sessionIds.Add(stacksResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 2/3 completed: Thread stacks collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 2/3 failed: Thread stacks collection failed - {Error}", stacksResult.Error);
        }

        // Step 3: Collect thread counters
        _logger.LogInformation("Step 3/3: Collecting thread counters for PID {Pid}", pid);
        var countersRequest = new CollectCountersRequest(pid, duration, Providers: _options.Value.DefaultCountersProviders);
        var countersResult = await _countersService.CollectCountersAsync(countersRequest, null, cancellationToken);
        if (countersResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(countersResult.Artifact.ArtifactId.Value, "counters", countersResult.Artifact.FilePath, countersResult.Artifact.SizeBytes));
            sessionIds.Add(countersResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 3/3 completed: Thread counters collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 3/3 failed: Thread counters collection failed - {Error}", countersResult.Error);
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
