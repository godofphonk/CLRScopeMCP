using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

// Placeholder CLI tools - NOT registered via MCP until implemented
// See Stage 0b for implementation plan
public sealed class CliCollectTools
{
    [McpServerTool(Name = "collect.counters", Title = "Collect Performance Counters", ReadOnly = false, Idempotent = false), Description("Сбор performance counters через dotnet-counters CLI (NOT YET IMPLEMENTED - placeholder for Stage 0b)")]
    public static Task<CollectCountersResult> CollectCounters(
        [Description("Process ID to collect counters from")] int pid,
        ICliCommandRunner cliRunner,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (pid <= 0)
        {
            throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
        }

        logger.LogWarning("collect.counters not yet implemented for PID {Pid}", pid);
        return Task.FromResult(new CollectCountersResult(
            Success: false,
            Message: "CLI fallback tools not yet implemented. This is a placeholder for Stage 0b.",
            ArtifactId: null
        ));
    }

    [McpServerTool(Name = "collect.gcdump", Title = "Collect GC Heap Snapshot", ReadOnly = false, Idempotent = false), Description("Сбор GC heap snapshot через dotnet-gcdump CLI (NOT YET IMPLEMENTED - placeholder for Stage 0b)")]
    public static Task<CollectGcDumpResult> CollectGcDump(
        [Description("Process ID to collect GC heap snapshot from")] int pid,
        ICliCommandRunner cliRunner,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (pid <= 0)
        {
            throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
        }

        logger.LogWarning("collect.gcdump not yet implemented for PID {Pid}", pid);
        return Task.FromResult(new CollectGcDumpResult(
            Success: false,
            Message: "CLI fallback tools not yet implemented. This is a placeholder for Stage 0b.",
            ArtifactId: null
        ));
    }

    [McpServerTool(Name = "collect.stacks", Title = "Collect Managed Stacks", ReadOnly = false, Idempotent = false), Description("Сбор managed stacks через dotnet-stack CLI (NOT YET IMPLEMENTED - placeholder for Stage 0b)")]
    public static Task<CollectStacksResult> CollectStacks(
        [Description("Process ID to collect managed stacks from")] int pid,
        ICliCommandRunner cliRunner,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (pid <= 0)
        {
            throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
        }

        logger.LogWarning("collect.stacks not yet implemented for PID {Pid}", pid);
        return Task.FromResult(new CollectStacksResult(
            Success: false,
            Message: "CLI fallback tools not yet implemented. This is a placeholder for Stage 0b.",
            ArtifactId: null
        ));
    }
}

public record CollectCountersResult(
    bool Success,
    string Message,
    string? ArtifactId
);

public record CollectGcDumpResult(
    bool Success,
    string Message,
    string? ArtifactId
);

public record CollectStacksResult(
    bool Success,
    string Message,
    string? ArtifactId
);
