using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Collect;

[McpServerToolType]
public sealed class CollectCountersTools
{
    [McpServerTool(Name = "collect_counters", Title = "Collect Performance Counters", ReadOnly = false, Idempotent = false), Description("Collect performance counters via dotnet-counters CLI")]
    public static async Task<CollectCountersResult> CollectCounters(
        [Description("Process ID to collect counters from")] int pid,
        McpServer server,
        [Description("Duration in hh:mm:ss format (e.g., 00:01:00 for 1 minute)")] string duration = "00:01:00",
        [Description("Counter providers (e.g., System.Runtime, Microsoft.AspNetCore.Hosting)")] string[]? providers = null,
        CancellationToken cancellationToken = default)
    {
        var countersService = server.Services!.GetRequiredService<CollectCountersService>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectCountersTools>>();

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

    [McpServerTool(Name = "collect_gcdump", Title = "Collect GC Heap Snapshot", ReadOnly = false, Idempotent = false), Description("Collect GC heap snapshot via dotnet-gcdump CLI")]
    public static async Task<CollectGcDumpResult> CollectGcDump(
        [Description("Process ID to collect GC heap snapshot from")] int pid,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var gcdumpService = server.Services!.GetRequiredService<CollectGcDumpService>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectCountersTools>>();

        try
        {
            if (pid <= 0)
            {
                throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
            }

            var request = new ClrScope.Mcp.Services.CollectGcDumpRequest(pid);
            var result = await gcdumpService.CollectGcDumpAsync(request, cancellationToken: cancellationToken);

            if (result.Artifact != null)
            {
                return new CollectGcDumpResult(
                    Success: true,
                    Message: $"GC heap snapshot collected successfully: {result.Artifact.ArtifactId.Value}",
                    ArtifactId: result.Artifact.ArtifactId.Value
                );
            }
            else
            {
                return new CollectGcDumpResult(
                    Success: false,
                    Message: result.Error ?? "GC heap snapshot collection failed",
                    ArtifactId: null
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collect GC heap snapshot failed for PID {Pid}", pid);
            return new CollectGcDumpResult(
                Success: false,
                Message: $"Collect GC heap snapshot failed: {ex.Message}",
                ArtifactId: null
            );
        }
    }

    [McpServerTool(Name = "collect_stacks", Title = "Collect Managed Stacks", ReadOnly = false, Idempotent = false), Description("Collect managed stacks via dotnet-stack CLI")]
    public static async Task<CollectStacksResult> CollectStacks(
        [Description("Process ID to collect managed stacks from")] int pid,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var stacksService = server.Services!.GetRequiredService<CollectStacksService>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectCountersTools>>();

        try
        {
            if (pid <= 0)
            {
                throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
            }

            var request = new ClrScope.Mcp.Services.CollectStacksRequest(pid);
            var result = await stacksService.CollectStacksAsync(request, cancellationToken: cancellationToken);

            if (result.Artifact != null)
            {
                return new CollectStacksResult(
                    Success: true,
                    Message: $"Managed stacks collected successfully: {result.Artifact.ArtifactId.Value}",
                    ArtifactId: result.Artifact.ArtifactId.Value
                );
            }
            else
            {
                return new CollectStacksResult(
                    Success: false,
                    Message: result.Error ?? "Managed stacks collection failed",
                    ArtifactId: null
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collect managed stacks failed for PID {Pid}", pid);
            return new CollectStacksResult(
                Success: false,
                Message: $"Collect managed stacks failed: {ex.Message}",
                ArtifactId: null
            );
        }
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
