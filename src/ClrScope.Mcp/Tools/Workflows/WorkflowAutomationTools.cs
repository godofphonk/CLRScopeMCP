using ClrScope.Mcp.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Workflows;

[McpServerToolType]
public sealed class WorkflowAutomationTools
{
    [McpServerTool(Name = "workflow_automated_high_cpu_bundle"), Description("Automated collection of high CPU diagnostic bundle - executes collect_trace, collect_counters, and collect_stacks in sequence")]
    public static async Task<Services.Workflows.WorkflowAutomationResult> AutomatedHighCpuBundle(
        [Description("Process ID to collect high CPU diagnostic bundle from")] int pid,
        McpServer server,
        [Description("Duration for trace and counters collection (hh:mm:ss format, default: 00:01:00)")] string duration = "00:01:00",
        CancellationToken cancellationToken = default)
    {
        var orchestrator = server.Services!.GetRequiredService<WorkflowOrchestrator>();
        var workflow = server.Services!.GetRequiredService<HighCpuWorkflow>();
        return await orchestrator.ExecuteWorkflowAsync(workflow, pid, duration, cancellationToken);
    }

    [McpServerTool(Name = "workflow_automated_memory_leak_bundle"), Description("Automated collection of memory leak diagnostic bundle - executes collect_gcdump, collect_counters, and collect_trace in sequence")]
    public static async Task<Services.Workflows.WorkflowAutomationResult> AutomatedMemoryLeakBundle(
        [Description("Process ID to collect memory leak diagnostic bundle from")] int pid,
        McpServer server,
        [Description("Duration for trace and counters collection (hh:mm:ss format, default: 00:01:00)")] string duration = "00:01:00",
        CancellationToken cancellationToken = default)
    {
        var orchestrator = server.Services!.GetRequiredService<WorkflowOrchestrator>();
        var workflow = server.Services!.GetRequiredService<MemoryLeakWorkflow>();
        return await orchestrator.ExecuteWorkflowAsync(workflow, pid, duration, cancellationToken);
    }

    [McpServerTool(Name = "workflow_automated_hang_bundle"), Description("Automated collection of hang/deadlock diagnostic bundle - executes collect_dump, collect_stacks, and collect_counters in sequence")]
    public static async Task<WorkflowAutomationResult> AutomatedHangBundle(
        [Description("Process ID to collect hang/deadlock diagnostic bundle from")] int pid,
        McpServer server,
        [Description("Duration for counters collection (hh:mm:ss format, default: 00:00:30)")] string duration = "00:00:30",
        CancellationToken cancellationToken = default)
    {
        var orchestrator = server.Services!.GetRequiredService<WorkflowOrchestrator>();
        var workflow = server.Services!.GetRequiredService<HangWorkflow>();
        return await orchestrator.ExecuteWorkflowAsync(workflow, pid, duration, cancellationToken);
    }

    [McpServerTool(Name = "workflow_automated_baseline_bundle"), Description("Automated collection of baseline performance bundle - executes collect_counters, collect_trace, collect_gcdump, and collect_stacks in sequence")]
    public static async Task<Services.Workflows.WorkflowAutomationResult> AutomatedBaselineBundle(
        [Description("Process ID to collect baseline performance bundle from")] int pid,
        McpServer server,
        [Description("Duration for trace and counters collection (hh:mm:ss format, default: 00:01:00)")] string duration = "00:01:00",
        CancellationToken cancellationToken = default)
    {
        var orchestrator = server.Services!.GetRequiredService<WorkflowOrchestrator>();
        var workflow = server.Services!.GetRequiredService<BaselineWorkflow>();
        return await orchestrator.ExecuteWorkflowAsync(workflow, pid, duration, cancellationToken);
    }
}
