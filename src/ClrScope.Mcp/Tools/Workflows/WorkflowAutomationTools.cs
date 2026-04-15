using ClrScope.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;

namespace ClrScope.Mcp.Tools.Workflows;

/*
// TODO: Workflow automation - postponed due to bugs
// Will be fixed and re-enabled later
[McpServerToolType]
public sealed class WorkflowAutomationTools
{
    [McpServerTool(Name = "workflow_automated_high_cpu_bundle"), Description("Automated collection of high CPU diagnostic bundle - executes collect_trace, collect_counters, and collect_stacks in sequence")]
    public static async Task<WorkflowAutomationResult> AutomatedHighCpuBundle(
        [Description("Process ID to collect high CPU diagnostic bundle from")] int pid,
        McpServer server,
        [Description("Duration for trace and counters collection (hh:mm:ss format, default: 00:01:00)")] string duration = "00:01:00",
        CancellationToken cancellationToken = default)
    {
        // ... implementation ...
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
*/
