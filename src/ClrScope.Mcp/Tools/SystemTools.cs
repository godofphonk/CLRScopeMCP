using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

[McpServerToolType]
public sealed class SystemTools
{
    [McpServerTool(Name = "system_health", Title = "System Health Check", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Check server health: artifact root, disk space, tool availability")]
    public static async Task<HealthCheckResult> SystemHealth(
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var healthService = server.Services!.GetRequiredService<HealthService>();
        var logger = server.Services!.GetRequiredService<ILogger<SystemTools>>();

        try
        {
            var health = await healthService.GetHealthAsync(cancellationToken);
            logger.LogInformation("Health check completed: IsHealthy={IsHealthy}", health.IsHealthy);

            return new HealthCheckResult(
                IsHealthy: health.IsHealthy,
                ArtifactRoot: health.ArtifactRoot.Path,
                FreeDiskSpaceBytes: health.ArtifactRoot.FreeSpaceBytes,
                DiagnosticsClientAvailable: true,
                Error: string.Empty
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed");
            return new HealthCheckResult(
                IsHealthy: false,
                ArtifactRoot: string.Empty,
                FreeDiskSpaceBytes: 0,
                DiagnosticsClientAvailable: false,
                Error: $"Health check failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "system_capabilities", Title = "System Capabilities", ReadOnly = true, Idempotent = true, UseStructuredContent = true), Description("Returns available capabilities and feature flags for the CLRScope MCP server")]
    public static async Task<CapabilitiesResult> GetCapabilities(
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var logger = server.Services!.GetRequiredService<ILogger<SystemTools>>();
        var toolChecker = server.Services!.GetRequiredService<ICliToolAvailabilityChecker>();
        
        logger.LogInformation("Capabilities requested");

        // Check CLI tool availability for advanced features
        var dotnetDumpTask = toolChecker.CheckAvailabilityAsync("dotnet-dump", cancellationToken);
        var dotnetSymbolTask = toolChecker.CheckAvailabilityAsync("dotnet-symbol", cancellationToken);
        
        await Task.WhenAll(dotnetDumpTask, dotnetSymbolTask);
        
        var dotnetDump = await dotnetDumpTask;
        var dotnetSymbol = await dotnetSymbolTask;

        return new CapabilitiesResult(
            NativeDumpAvailable: true,
            NativeTraceAvailable: true,
            NativeCountersAvailable: true,
            TraceStatus: "stable",
            DotnetDumpAvailable: dotnetDump.IsAvailable,
            DotnetDumpVersion: dotnetDump.Version,
            DotnetDumpInstallHint: dotnetDump.InstallHint,
            DotnetSymbolAvailable: dotnetSymbol.IsAvailable,
            DotnetSymbolVersion: dotnetSymbol.Version,
            DotnetSymbolInstallHint: dotnetSymbol.InstallHint,
            ResourcesEnabled: false,
            PromptsEnabled: false
        );
    }
}

public record HealthCheckResult(
    bool IsHealthy,
    string ArtifactRoot,
    long FreeDiskSpaceBytes,
    bool DiagnosticsClientAvailable,
    string? Error
);

public record CapabilitiesResult(
    bool NativeDumpAvailable,
    bool NativeTraceAvailable,
    bool NativeCountersAvailable,
    string TraceStatus,
    bool DotnetDumpAvailable,
    string? DotnetDumpVersion,
    string? DotnetDumpInstallHint,
    bool DotnetSymbolAvailable,
    string? DotnetSymbolVersion,
    string? DotnetSymbolInstallHint,
    bool ResourcesEnabled,
    bool PromptsEnabled
);
