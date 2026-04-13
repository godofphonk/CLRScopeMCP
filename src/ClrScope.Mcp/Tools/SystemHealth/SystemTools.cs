using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.SystemHealth;

[McpServerToolType]
public sealed class SystemTools
{
    [McpServerTool(Name = "system_health", Title = "System Health Check", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Check server health: artifact root, disk space, tool availability")]
    public static async Task<HealthCheckResult> SystemHealth(
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var healthService = server.Services!.GetRequiredService<HealthService>();
        var runtimeService = server.Services!.GetRequiredService<RuntimeService>();
        var logger = server.Services!.GetRequiredService<ILogger<SystemTools>>();

        try
        {
            var health = await healthService.GetHealthAsync(cancellationToken);
            
            // Check DiagnosticsClient availability by trying to list .NET processes
            var diagnosticsClientAvailable = false;
            try
            {
                runtimeService.ListTargets(); // If this doesn't throw, DiagnosticsClient is available
                diagnosticsClientAvailable = true;
            }
            catch
            {
                diagnosticsClientAvailable = false;
            }
            
            logger.LogInformation("Health check completed: IsHealthy={IsHealthy}, DiagnosticsClientAvailable={DiagnosticsClientAvailable}", 
                health.IsHealthy, diagnosticsClientAvailable);

            return new HealthCheckResult(
                IsHealthy: health.IsHealthy,
                ArtifactRoot: health.ArtifactRoot.Path,
                FreeDiskSpaceBytes: health.ArtifactRoot.FreeSpaceBytes,
                DiagnosticsClientAvailable: diagnosticsClientAvailable,
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
        var runtimeService = server.Services!.GetRequiredService<RuntimeService>();
        
        logger.LogInformation("Capabilities requested");

        // Check CLI tool availability for advanced features
        var dotnetDumpTask = toolChecker.CheckAvailabilityAsync("dotnet-dump", cancellationToken);
        var dotnetSymbolTask = toolChecker.CheckAvailabilityAsync("dotnet-symbol", cancellationToken);
        var dotnetGcdumpTask = toolChecker.CheckAvailabilityAsync("dotnet-gcdump", cancellationToken);
        var dotnetStackTask = toolChecker.CheckAvailabilityAsync("dotnet-stack", cancellationToken);
        var dotnetCountersTask = toolChecker.CheckAvailabilityAsync("dotnet-counters", cancellationToken);
        
        await Task.WhenAll(dotnetDumpTask, dotnetSymbolTask, dotnetGcdumpTask, dotnetStackTask, dotnetCountersTask);
        
        var dotnetDump = await dotnetDumpTask;
        var dotnetSymbol = await dotnetSymbolTask;
        var dotnetGcdump = await dotnetGcdumpTask;
        var dotnetStack = await dotnetStackTask;
        var dotnetCounters = await dotnetCountersTask;

        // Check native capabilities (DiagnosticsClient API availability)
        // Native capabilities depend on runtime support, not on currently running processes
        var nativeDumpAvailable = true; // DiagnosticsClient is always available if runtime is installed
        var nativeTraceAvailable = true; // DiagnosticsClient is always available if runtime is installed

        return new CapabilitiesResult(
            NativeDumpAvailable: nativeDumpAvailable,
            NativeTraceAvailable: nativeTraceAvailable,
            NativeCountersAvailable: false, // Native counters not implemented
            TraceStatus: "stable",
            DotnetDumpAvailable: dotnetDump.IsAvailable,
            DotnetDumpVersion: dotnetDump.Version,
            DotnetDumpInstallHint: dotnetDump.InstallHint,
            DotnetSymbolAvailable: dotnetSymbol.IsAvailable,
            DotnetSymbolVersion: dotnetSymbol.Version,
            DotnetSymbolInstallHint: dotnetSymbol.InstallHint,
            DotnetGcdumpAvailable: dotnetGcdump.IsAvailable,
            DotnetGcdumpVersion: dotnetGcdump.Version,
            DotnetGcdumpInstallHint: dotnetGcdump.InstallHint,
            DotnetStackAvailable: dotnetStack.IsAvailable,
            DotnetStackVersion: dotnetStack.Version,
            DotnetStackInstallHint: dotnetStack.InstallHint,
            DotnetCountersAvailable: dotnetCounters.IsAvailable,
            DotnetCountersVersion: dotnetCounters.Version,
            DotnetCountersInstallHint: dotnetCounters.InstallHint,
            ResourcesEnabled: true,
            PromptsEnabled: true
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
    bool DotnetGcdumpAvailable,
    string? DotnetGcdumpVersion,
    string? DotnetGcdumpInstallHint,
    bool DotnetStackAvailable,
    string? DotnetStackVersion,
    string? DotnetStackInstallHint,
    bool DotnetCountersAvailable,
    string? DotnetCountersVersion,
    string? DotnetCountersInstallHint,
    bool ResourcesEnabled,
    bool PromptsEnabled
);
