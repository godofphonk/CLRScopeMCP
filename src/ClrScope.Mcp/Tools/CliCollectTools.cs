using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

public sealed class CollectCountersTools
{
    [McpServerTool(Name = "collect_counters", Title = "Collect Performance Counters", ReadOnly = false, Idempotent = false), Description("Collect performance counters via native EventPipe (Stage 2)")]
    public static async Task<CollectCountersResult> CollectCounters(
        [Description("Process ID to collect counters from")] int pid,
        CollectCountersService countersService,
        ILogger logger,
        [Description("Duration in hh:mm:ss format (e.g., 00:01:00 for 1 minute)")] string duration = "00:01:00",
        [Description("Counter providers (e.g., System.Runtime, Microsoft.AspNetCore.Hosting)")] string[]? providers = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pid <= 0)
            {
                throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
            }

            var providerList = providers != null && providers.Length > 0 ? providers : new[] { "System.Runtime" };
            var request = new CollectCountersRequest(pid, duration, providerList);
            var result = await countersService.CollectCountersAsync(request, cancellationToken: cancellationToken);

            if (result.Artifact != null)
            {
                return new CollectCountersResult(
                    Success: true,
                    Message: $"Counters collected successfully: {result.Artifact.ArtifactId.Value}",
                    ArtifactId: result.Artifact.ArtifactId.Value,
                    SessionId: result.Session.SessionId.Value
                );
            }
            else
            {
                return new CollectCountersResult(
                    Success: false,
                    Message: result.Error ?? "Counter collection failed",
                    ArtifactId: null,
                    SessionId: result.Session.SessionId.Value
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collect counters failed for PID {Pid}", pid);
            return new CollectCountersResult(
                Success: false,
                Message: $"Collect counters failed: {ex.Message}",
                ArtifactId: null,
                SessionId: null
            );
        }
    }

    [McpServerTool(Name = "collect_gcdump", Title = "Collect GC Heap Snapshot", ReadOnly = false, Idempotent = false), Description("Collect GC heap snapshot via dotnet-gcdump CLI (NOT YET IMPLEMENTED - placeholder for Stage 0b)")]
    public static Task<CollectGcDumpResult> CollectGcDump(
        [Description("Process ID to collect GC heap snapshot from")] int pid,
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

    [McpServerTool(Name = "collect_stacks", Title = "Collect Managed Stacks", ReadOnly = false, Idempotent = false), Description("Collect managed stacks via dotnet-stack CLI (NOT YET IMPLEMENTED - placeholder for Stage 0b)")]
    public static Task<CollectStacksResult> CollectStacks(
        [Description("Process ID to collect managed stacks from")] int pid,
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
    string? ArtifactId,
    string? SessionId = null
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
