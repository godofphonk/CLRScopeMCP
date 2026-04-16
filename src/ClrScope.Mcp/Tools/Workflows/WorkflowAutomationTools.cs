using ClrScope.Mcp.Services.Collect;
using ClrScope.Mcp.Services.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;

namespace ClrScope.Mcp.Tools.Workflows;

// TODO: Workflow automation - re-enabled for testing
[McpServerToolType]
public sealed class WorkflowAutomationTools
{
    private static readonly SemaphoreSlim _cliSemaphore = new SemaphoreSlim(1, 1);

    [McpServerTool(Name = "workflow_automated_high_cpu_bundle"), Description("Automated collection of high CPU diagnostic bundle - executes collect_trace, collect_counters, and collect_stacks in sequence")]
    public static async Task<WorkflowAutomationResult> AutomatedHighCpuBundle(
        [Description("Process ID to collect high CPU diagnostic bundle from")] int pid,
        McpServer server,
        [Description("Duration for trace and counters collection (hh:mm:ss format, default: 00:01:00)")] string duration = "00:01:00",
        CancellationToken cancellationToken = default)
    {
        await _cliSemaphore.WaitAsync(cancellationToken);
        var logger = server.Services!.GetRequiredService<ILogger<WorkflowAutomationTools>>();
        var startTime = DateTime.UtcNow;
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();
        int stepsCompleted = 0;
        int totalSteps = 3;

        try
        {
            logger.LogInformation("Starting automated high CPU bundle collection for PID {Pid}", pid);
            // Step 1: Collect trace
            logger.LogInformation("Step 1/3: Collecting trace for PID {Pid}", pid);
            var traceService = server.Services!.GetRequiredService<CollectTraceService>();
            var traceRequest = new CollectTraceRequest(pid, duration, Profile: "cpu-sampling");
            var traceResult = await traceService.CollectTraceAsync(traceRequest, null, cancellationToken);
            if (traceResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(traceResult.Artifact.ArtifactId.Value, "trace", traceResult.Artifact.FilePath, traceResult.Artifact.SizeBytes));
                sessionIds.Add(traceResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 1/3 completed: Trace collected successfully");
            }
            else
            {
                logger.LogWarning("Step 1/3 failed: Trace collection failed - {Error}", traceResult.Error);
            }

            // Step 2: Collect counters
            logger.LogInformation("Step 2/3: Collecting counters for PID {Pid}", pid);
            var countersService = server.Services!.GetRequiredService<CollectCountersService>();
            var countersRequest = new CollectCountersRequest(pid, duration, Providers: new[] { "System.Runtime" });
            var countersResult = await countersService.CollectCountersAsync(countersRequest, null, cancellationToken);
            if (countersResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(countersResult.Artifact.ArtifactId.Value, "counters", countersResult.Artifact.FilePath, countersResult.Artifact.SizeBytes));
                sessionIds.Add(countersResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 2/3 completed: Counters collected successfully");
            }
            else
            {
                logger.LogWarning("Step 2/3 failed: Counters collection failed - {Error}", countersResult.Error);
            }

            // Step 3: Collect stacks
            logger.LogInformation("Step 3/3: Collecting stacks for PID {Pid}", pid);
            var stacksService = server.Services!.GetRequiredService<CollectStacksService>();
            var stacksRequest = new CollectStacksRequest(pid);
            var stacksResult = await stacksService.CollectStacksAsync(stacksRequest, null, cancellationToken);
            if (stacksResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(stacksResult.Artifact.ArtifactId.Value, "stacks", stacksResult.Artifact.FilePath, stacksResult.Artifact.SizeBytes));
                sessionIds.Add(stacksResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 3/3 completed: Stacks collected successfully");
            }
            else
            {
                logger.LogWarning("Step 3/3 failed: Stacks collection failed - {Error}", stacksResult.Error);
            }

            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var success = stepsCompleted == totalSteps;
            var error = success ? null : $"Completed {stepsCompleted}/{totalSteps} steps";

            logger.LogInformation("Automated high CPU bundle collection completed: {StepsCompleted}/{TotalSteps} steps, {ExecutionTimeMs}ms", stepsCompleted, totalSteps, executionTimeMs);

            return new WorkflowAutomationResult(
                Success: success,
                WorkflowName: "automated_high_cpu_bundle",
                StepsCompleted: stepsCompleted,
                TotalSteps: totalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: error,
                ExecutionTimeMs: executionTimeMs
            );
        }
        catch (Exception ex)
        {
            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            logger.LogError(ex, "Automated high CPU bundle collection failed for PID {Pid}", pid);
            return new WorkflowAutomationResult(
                Success: false,
                WorkflowName: "automated_high_cpu_bundle",
                StepsCompleted: stepsCompleted,
                TotalSteps: totalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: ex.Message,
                ExecutionTimeMs: executionTimeMs
            );
        }
        finally
        {
            _cliSemaphore.Release();
        }
    }

    [McpServerTool(Name = "workflow_automated_memory_leak_bundle"), Description("Automated collection of memory leak diagnostic bundle - executes collect_gcdump, collect_counters, and collect_trace in sequence")]
    public static async Task<WorkflowAutomationResult> AutomatedMemoryLeakBundle(
        [Description("Process ID to collect memory leak diagnostic bundle from")] int pid,
        McpServer server,
        [Description("Duration for trace and counters collection (hh:mm:ss format, default: 00:01:00)")] string duration = "00:01:00",
        CancellationToken cancellationToken = default)
    {
        await _cliSemaphore.WaitAsync(cancellationToken);
        var logger = server.Services!.GetRequiredService<ILogger<WorkflowAutomationTools>>();
        var startTime = DateTime.UtcNow;
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();
        int stepsCompleted = 0;
        int totalSteps = 3;

        try
        {
            logger.LogInformation("Starting automated memory leak bundle collection for PID {Pid}", pid);
            // Step 1: Collect GC dump
            logger.LogInformation("Step 1/3: Collecting GC dump for PID {Pid}", pid);
            var gcdumpService = server.Services!.GetRequiredService<CollectGcDumpService>();
            var gcdumpRequest = new CollectGcDumpRequest(pid);
            var gcdumpResult = await gcdumpService.CollectGcDumpAsync(gcdumpRequest, null, cancellationToken);
            if (gcdumpResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(gcdumpResult.Artifact.ArtifactId.Value, "gcdump", gcdumpResult.Artifact.FilePath, gcdumpResult.Artifact.SizeBytes));
                sessionIds.Add(gcdumpResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 1/3 completed: GC dump collected successfully");
            }
            else
            {
                logger.LogWarning("Step 1/3 failed: GC dump collection failed - {Error}", gcdumpResult.Error);
            }

            // Step 2: Collect GC counters
            logger.LogInformation("Step 2/3: Collecting GC counters for PID {Pid}", pid);
            var countersService = server.Services!.GetRequiredService<CollectCountersService>();
            var countersRequest = new CollectCountersRequest(pid, duration, Providers: new[] { "System.Runtime" });
            var countersResult = await countersService.CollectCountersAsync(countersRequest, null, cancellationToken);
            if (countersResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(countersResult.Artifact.ArtifactId.Value, "counters", countersResult.Artifact.FilePath, countersResult.Artifact.SizeBytes));
                sessionIds.Add(countersResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 2/3 completed: GC counters collected successfully");
            }
            else
            {
                logger.LogWarning("Step 2/3 failed: GC counters collection failed - {Error}", countersResult.Error);
            }

            // Step 3: Collect GC heap trace
            logger.LogInformation("Step 3/3: Collecting GC heap trace for PID {Pid}", pid);
            var traceService = server.Services!.GetRequiredService<CollectTraceService>();
            var traceRequest = new CollectTraceRequest(pid, duration, Profile: "gc-heap");
            var traceResult = await traceService.CollectTraceAsync(traceRequest, null, cancellationToken);
            if (traceResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(traceResult.Artifact.ArtifactId.Value, "trace", traceResult.Artifact.FilePath, traceResult.Artifact.SizeBytes));
                sessionIds.Add(traceResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 3/3 completed: GC heap trace collected successfully");
            }
            else
            {
                logger.LogWarning("Step 3/3 failed: GC heap trace collection failed - {Error}", traceResult.Error);
            }

            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var success = stepsCompleted == totalSteps;
            var error = success ? null : $"Completed {stepsCompleted}/{totalSteps} steps";

            logger.LogInformation("Automated memory leak bundle collection completed: {StepsCompleted}/{TotalSteps} steps, {ExecutionTimeMs}ms", stepsCompleted, totalSteps, executionTimeMs);

            return new WorkflowAutomationResult(
                Success: success,
                WorkflowName: "automated_memory_leak_bundle",
                StepsCompleted: stepsCompleted,
                TotalSteps: totalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: error,
                ExecutionTimeMs: executionTimeMs
            );
        }
        catch (Exception ex)
        {
            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            logger.LogError(ex, "Automated memory leak bundle collection failed for PID {Pid}", pid);
            return new WorkflowAutomationResult(
                Success: false,
                WorkflowName: "automated_memory_leak_bundle",
                StepsCompleted: stepsCompleted,
                TotalSteps: totalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: ex.Message,
                ExecutionTimeMs: executionTimeMs
            );
        }
        finally
        {
            _cliSemaphore.Release();
        }
    }

    [McpServerTool(Name = "workflow_automated_hang_bundle"), Description("Automated collection of hang/deadlock diagnostic bundle - executes collect_dump, collect_stacks, and collect_counters in sequence")]
    public static async Task<WorkflowAutomationResult> AutomatedHangBundle(
        [Description("Process ID to collect hang/deadlock diagnostic bundle from")] int pid,
        McpServer server,
        [Description("Duration for counters collection (hh:mm:ss format, default: 00:00:30)")] string duration = "00:00:30",
        CancellationToken cancellationToken = default)
    {
        await _cliSemaphore.WaitAsync(cancellationToken);
        var logger = server.Services!.GetRequiredService<ILogger<WorkflowAutomationTools>>();
        var startTime = DateTime.UtcNow;
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();
        int stepsCompleted = 0;
        int totalSteps = 3;

        try
        {
            logger.LogInformation("Starting automated hang bundle collection for PID {Pid}", pid);
            // Step 1: Collect memory dump
            logger.LogInformation("Step 1/3: Collecting memory dump for PID {Pid}", pid);
            var dumpService = server.Services!.GetRequiredService<CollectDumpService>();
            var dumpRequest = new CollectDumpRequest(pid, IncludeHeap: true, Compress: false);
            var dumpResult = await dumpService.CollectDumpAsync(dumpRequest, null, cancellationToken);
            if (dumpResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(dumpResult.Artifact.ArtifactId.Value, "dump", dumpResult.Artifact.FilePath, dumpResult.Artifact.SizeBytes));
                sessionIds.Add(dumpResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 1/3 completed: Memory dump collected successfully");
            }
            else
            {
                logger.LogWarning("Step 1/3 failed: Memory dump collection failed - {Error}", dumpResult.Error);
            }

            // Step 2: Collect thread stacks
            logger.LogInformation("Step 2/3: Collecting thread stacks for PID {Pid}", pid);
            var stacksService = server.Services!.GetRequiredService<CollectStacksService>();
            var stacksRequest = new CollectStacksRequest(pid);
            var stacksResult = await stacksService.CollectStacksAsync(stacksRequest, null, cancellationToken);
            if (stacksResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(stacksResult.Artifact.ArtifactId.Value, "stacks", stacksResult.Artifact.FilePath, stacksResult.Artifact.SizeBytes));
                sessionIds.Add(stacksResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 2/3 completed: Thread stacks collected successfully");
            }
            else
            {
                logger.LogWarning("Step 2/3 failed: Thread stacks collection failed - {Error}", stacksResult.Error);
            }

            // Step 3: Collect thread counters
            logger.LogInformation("Step 3/3: Collecting thread counters for PID {Pid}", pid);
            var countersService = server.Services!.GetRequiredService<CollectCountersService>();
            var countersRequest = new CollectCountersRequest(pid, duration, Providers: new[] { "System.Runtime" });
            var countersResult = await countersService.CollectCountersAsync(countersRequest, null, cancellationToken);
            if (countersResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(countersResult.Artifact.ArtifactId.Value, "counters", countersResult.Artifact.FilePath, countersResult.Artifact.SizeBytes));
                sessionIds.Add(countersResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 3/3 completed: Thread counters collected successfully");
            }
            else
            {
                logger.LogWarning("Step 3/3 failed: Thread counters collection failed - {Error}", countersResult.Error);
            }

            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var success = stepsCompleted == totalSteps;
            var error = success ? null : $"Completed {stepsCompleted}/{totalSteps} steps";

            logger.LogInformation("Automated hang bundle collection completed: {StepsCompleted}/{TotalSteps} steps, {ExecutionTimeMs}ms", stepsCompleted, totalSteps, executionTimeMs);

            return new WorkflowAutomationResult(
                Success: success,
                WorkflowName: "automated_hang_bundle",
                StepsCompleted: stepsCompleted,
                TotalSteps: totalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: error,
                ExecutionTimeMs: executionTimeMs
            );
        }
        catch (Exception ex)
        {
            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            logger.LogError(ex, "Automated hang bundle collection failed for PID {Pid}", pid);
            return new WorkflowAutomationResult(
                Success: false,
                WorkflowName: "automated_hang_bundle",
                StepsCompleted: stepsCompleted,
                TotalSteps: totalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: ex.Message,
                ExecutionTimeMs: executionTimeMs
            );
        }
        finally
        {
            _cliSemaphore.Release();
        }
    }

    [McpServerTool(Name = "workflow_automated_baseline_bundle"), Description("Automated collection of baseline performance bundle - executes collect_counters, collect_trace, collect_gcdump, and collect_stacks in sequence")]
    public static async Task<WorkflowAutomationResult> AutomatedBaselineBundle(
        [Description("Process ID to collect baseline performance bundle from")] int pid,
        McpServer server,
        [Description("Duration for trace and counters collection (hh:mm:ss format, default: 00:01:00)")] string duration = "00:01:00",
        CancellationToken cancellationToken = default)
    {
        await _cliSemaphore.WaitAsync(cancellationToken);
        var logger = server.Services!.GetRequiredService<ILogger<WorkflowAutomationTools>>();
        var startTime = DateTime.UtcNow;
        var artifacts = new List<ArtifactInfo>();
        var sessionIds = new List<string>();
        int stepsCompleted = 0;
        int totalSteps = 4;

        try
        {
            logger.LogInformation("Starting automated baseline bundle collection for PID {Pid}", pid);
            // Step 1: Collect performance counters
            logger.LogInformation("Step 1/4: Collecting performance counters for PID {Pid}", pid);
            var countersService = server.Services!.GetRequiredService<CollectCountersService>();
            var countersRequest = new CollectCountersRequest(pid, duration, Providers: new[] { "System.Runtime" });
            var countersResult = await countersService.CollectCountersAsync(countersRequest, null, cancellationToken);
            if (countersResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(countersResult.Artifact.ArtifactId.Value, "counters", countersResult.Artifact.FilePath, countersResult.Artifact.SizeBytes));
                sessionIds.Add(countersResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 1/4 completed: Performance counters collected successfully");
            }
            else
            {
                logger.LogWarning("Step 1/4 failed: Performance counters collection failed - {Error}", countersResult.Error);
            }

            // Step 2: Collect baseline trace
            logger.LogInformation("Step 2/4: Collecting baseline trace for PID {Pid}", pid);
            var traceService = server.Services!.GetRequiredService<CollectTraceService>();
            var traceRequest = new CollectTraceRequest(pid, duration, Profile: "cpu-sampling");
            var traceResult = await traceService.CollectTraceAsync(traceRequest, null, cancellationToken);
            if (traceResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(traceResult.Artifact.ArtifactId.Value, "trace", traceResult.Artifact.FilePath, traceResult.Artifact.SizeBytes));
                sessionIds.Add(traceResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 2/4 completed: Baseline trace collected successfully");
            }
            else
            {
                logger.LogWarning("Step 2/4 failed: Baseline trace collection failed - {Error}", traceResult.Error);
            }

            // Step 3: Collect GC dump
            logger.LogInformation("Step 3/4: Collecting GC dump for PID {Pid}", pid);
            var gcdumpService = server.Services!.GetRequiredService<CollectGcDumpService>();
            var gcdumpRequest = new CollectGcDumpRequest(pid);
            var gcdumpResult = await gcdumpService.CollectGcDumpAsync(gcdumpRequest, null, cancellationToken);
            if (gcdumpResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(gcdumpResult.Artifact.ArtifactId.Value, "gcdump", gcdumpResult.Artifact.FilePath, gcdumpResult.Artifact.SizeBytes));
                sessionIds.Add(gcdumpResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 3/4 completed: GC dump collected successfully");
            }
            else
            {
                logger.LogWarning("Step 3/4 failed: GC dump collection failed - {Error}", gcdumpResult.Error);
            }

            // Step 4: Collect thread stacks
            logger.LogInformation("Step 4/4: Collecting thread stacks for PID {Pid}", pid);
            var stacksService = server.Services!.GetRequiredService<CollectStacksService>();
            var stacksRequest = new CollectStacksRequest(pid);
            var stacksResult = await stacksService.CollectStacksAsync(stacksRequest, null, cancellationToken);
            if (stacksResult.Artifact != null)
            {
                artifacts.Add(new ArtifactInfo(stacksResult.Artifact.ArtifactId.Value, "stacks", stacksResult.Artifact.FilePath, stacksResult.Artifact.SizeBytes));
                sessionIds.Add(stacksResult.Session.SessionId.Value);
                stepsCompleted++;
                logger.LogInformation("Step 4/4 completed: Thread stacks collected successfully");
            }
            else
            {
                logger.LogWarning("Step 4/4 failed: Thread stacks collection failed - {Error}", stacksResult.Error);
            }

            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var success = stepsCompleted == totalSteps;
            var error = success ? null : $"Completed {stepsCompleted}/{totalSteps} steps";

            logger.LogInformation("Automated baseline bundle collection completed: {StepsCompleted}/{TotalSteps} steps, {ExecutionTimeMs}ms", stepsCompleted, totalSteps, executionTimeMs);

            return new WorkflowAutomationResult(
                Success: success,
                WorkflowName: "automated_baseline_bundle",
                StepsCompleted: stepsCompleted,
                TotalSteps: totalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: error,
                ExecutionTimeMs: executionTimeMs
            );
        }
        catch (Exception ex)
        {
            var executionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            logger.LogError(ex, "Automated baseline bundle collection failed for PID {Pid}", pid);
            return new WorkflowAutomationResult(
                Success: false,
                WorkflowName: "automated_baseline_bundle",
                StepsCompleted: stepsCompleted,
                TotalSteps: totalSteps,
                Artifacts: artifacts.ToArray(),
                SessionIds: sessionIds.ToArray(),
                Error: ex.Message,
                ExecutionTimeMs: executionTimeMs
            );
        }
        finally
        {
            _cliSemaphore.Release();
        }
    }
}

public record WorkflowAutomationResult(
    bool Success,
    string WorkflowName,
    int StepsCompleted,
    int TotalSteps,
    ArtifactInfo[] Artifacts,
    string[] SessionIds,
    string? Error,
    long ExecutionTimeMs
);

public record ArtifactInfo(
    string ArtifactId,
    string Kind,
    string? FilePath,
    long SizeBytes
);
