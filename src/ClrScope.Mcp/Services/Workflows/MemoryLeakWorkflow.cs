using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Collect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Services.Workflows;

/// <summary>
/// Automated memory leak diagnostic workflow - collects gcdump, counters, and trace
/// </summary>
public sealed class MemoryLeakWorkflow : IWorkflow
{
    private readonly ILogger<MemoryLeakWorkflow> _logger;
    private readonly CollectGcDumpService _gcdumpService;
    private readonly CollectCountersService _countersService;
    private readonly CollectTraceService _traceService;
    private readonly IOptions<ClrScopeOptions> _options;

    public MemoryLeakWorkflow(
        ILogger<MemoryLeakWorkflow> logger,
        CollectGcDumpService gcdumpService,
        CollectCountersService countersService,
        CollectTraceService traceService,
        IOptions<ClrScopeOptions> options)
    {
        _logger = logger;
        _gcdumpService = gcdumpService;
        _countersService = countersService;
        _traceService = traceService;
        _options = options;
    }

    public string WorkflowName => "automated_memory_leak_bundle";
    public int TotalSteps => 3;

    public async Task<WorkflowAutomationResult> ExecuteAsync(
        int pid,
        string duration,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();
        int stepsCompleted = 0;

        // Step 1: Collect GC dump
        _logger.LogInformation("Step 1/3: Collecting GC dump for PID {Pid}", pid);
        var gcdumpRequest = new CollectGcDumpRequest(pid);
        var gcdumpResult = await _gcdumpService.CollectGcDumpAsync(gcdumpRequest, null, cancellationToken);
        if (gcdumpResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(gcdumpResult.Artifact.ArtifactId.Value, "gcdump", gcdumpResult.Artifact.FilePath, gcdumpResult.Artifact.SizeBytes));
            sessionIds.Add(gcdumpResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 1/3 completed: GC dump collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 1/3 failed: GC dump collection failed - {Error}", gcdumpResult.Error);
        }

        // Step 2: Collect GC counters
        _logger.LogInformation("Step 2/3: Collecting GC counters for PID {Pid}", pid);
        var countersRequest = new CollectCountersRequest(pid, duration, Providers: _options.Value.DefaultCountersProviders);
        var countersResult = await _countersService.CollectCountersAsync(countersRequest, null, cancellationToken);
        if (countersResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(countersResult.Artifact.ArtifactId.Value, "counters", countersResult.Artifact.FilePath, countersResult.Artifact.SizeBytes));
            sessionIds.Add(countersResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 2/3 completed: GC counters collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 2/3 failed: GC counters collection failed - {Error}", countersResult.Error);
        }

        // Step 3: Collect GC heap trace
        _logger.LogInformation("Step 3/3: Collecting GC heap trace for PID {Pid}", pid);
        var traceRequest = new CollectTraceRequest(pid, duration, Profile: "gc-heap");
        var traceResult = await _traceService.CollectTraceAsync(traceRequest, null, cancellationToken);
        if (traceResult.Artifact != null)
        {
            artifacts.Add(new ArtifactInfo(traceResult.Artifact.ArtifactId.Value, "trace", traceResult.Artifact.FilePath, traceResult.Artifact.SizeBytes));
            sessionIds.Add(traceResult.Session.SessionId.Value);
            stepsCompleted++;
            _logger.LogInformation("Step 3/3 completed: GC heap trace collected successfully");
        }
        else
        {
            _logger.LogWarning("Step 3/3 failed: GC heap trace collection failed - {Error}", traceResult.Error);
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
