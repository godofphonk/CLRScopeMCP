using ClrScope.Mcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

[McpServerToolType]
public sealed class SystemTools
{
    private readonly HealthService _healthService;
    private readonly ILogger<SystemTools> _logger;

    public SystemTools(HealthService healthService, ILogger<SystemTools> logger)
    {
        _healthService = healthService;
        _logger = logger;
    }

    [McpServerTool(Name = "system.health", Title = "System Health Check", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Проверка здоровья сервера: artifact root, disk space, доступность инструментов")]
    public async Task<HealthCheckResult> SystemHealth(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _healthService.GetHealthAsync(cancellationToken);
            _logger.LogInformation("Health check completed: IsHealthy={IsHealthy}", health.IsHealthy);

            return new HealthCheckResult(
                IsHealthy: health.IsHealthy,
                ArtifactRoot: health.ArtifactRoot.Path,
                FreeDiskSpaceBytes: health.ArtifactRoot.FreeSpaceBytes,
                DiagnosticsClientAvailable: true,
                Error: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return new HealthCheckResult(
                IsHealthy: false,
                ArtifactRoot: string.Empty,
                FreeDiskSpaceBytes: 0,
                DiagnosticsClientAvailable: false,
                Error: $"Health check failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "system.capabilities", Title = "System Capabilities", ReadOnly = true, Idempotent = true, UseStructuredContent = true), Description("Returns available capabilities and feature flags for the CLRScope MCP server")]
    public CapabilitiesResult GetCapabilities()
    {
        _logger.LogInformation("Capabilities requested");

        var dotnetCountersAvailable = CheckCliToolAvailable("dotnet-counters");
        var dotnetGcDumpAvailable = CheckCliToolAvailable("dotnet-gcdump");
        var dotnetStackAvailable = CheckCliToolAvailable("dotnet-stack");

        return new CapabilitiesResult(
            NativeDumpAvailable: true,
            NativeTraceAvailable: true,
            TraceStatus: "experimental",
            DotnetCountersInstalled: dotnetCountersAvailable,
            DotnetGcDumpInstalled: dotnetGcDumpAvailable,
            DotnetStackInstalled: dotnetStackAvailable,
            ResourcesEnabled: false,
            PromptsEnabled: false
        );
    }

    private static bool CheckCliToolAvailable(string toolName)
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = toolName,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
                return false;

            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
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
    string TraceStatus,
    bool DotnetCountersInstalled,
    bool DotnetGcDumpInstalled,
    bool DotnetStackInstalled,
    bool ResourcesEnabled,
    bool PromptsEnabled
);
