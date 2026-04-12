using ClrScope.Mcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

[McpServerToolType]
public sealed class RuntimeTools
{
    private readonly RuntimeService _runtimeService;
    private readonly InspectTargetService _inspectService;
    private readonly ILogger<RuntimeTools> _logger;

    public RuntimeTools(RuntimeService runtimeService, InspectTargetService inspectService, ILogger<RuntimeTools> logger)
    {
        _runtimeService = runtimeService;
        _inspectService = inspectService;
        _logger = logger;
    }

    [McpServerTool(Name = "runtime.list_targets", Title = "List .NET Processes", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Список всех attachable .NET процессов через GetPublishedProcesses()")]
    public ListTargetsResult ListTargets(CancellationToken cancellationToken = default)
    {
        try
        {
            var targets = _runtimeService.ListTargets();
            _logger.LogInformation("Found {Count} .NET processes", targets.Count);
            
            return new ListTargetsResult(
                Targets: targets.Select(t => new RuntimeTargetInfo(t.Pid, t.ProcessName)).ToArray(),
                Count: targets.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List targets failed");
            throw new InvalidOperationException("List targets failed", ex);
        }
    }

    [McpServerTool(Name = "runtime.inspect_target", Title = "Inspect .NET Process", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Детальная информация о .NET процессе. Возвращает guaranteed fields (pid, processName, isAttachable) и best-effort fields (commandLine, OS, architecture).")]
    public InspectTargetResult InspectTarget(
        [Description("Process ID to inspect")] int pid,
        CancellationToken cancellationToken = default)
    {
        if (pid <= 0)
        {
            throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
        }

        try
        {
            var result = _inspectService.InspectTarget(pid);
            
            if (!result.Found)
            {
                _logger.LogWarning("Process {Pid} not found", pid);
                return new InspectTargetResult(
                    Found: false,
                    Attachable: false,
                    ProcessName: null,
                    CommandLine: null,
                    OperatingSystem: null,
                    ProcessArchitecture: null,
                    Warnings: Array.Empty<string>(),
                    Error: result.Error
                );
            }
            
            _logger.LogInformation("Inspected process {Pid}: Found={Found}, Attachable={Attachable}", pid, result.Found, result.Attachable);
            
            return new InspectTargetResult(
                Found: result.Found,
                Attachable: result.Attachable,
                ProcessName: result.Details?.ProcessName,
                CommandLine: result.Details?.CommandLine,
                OperatingSystem: result.Details?.OperatingSystem,
                ProcessArchitecture: result.Details?.ProcessArchitecture,
                Warnings: result.Warnings,
                Error: result.Error
            );
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid input for target inspection: {Message}", ex.Message);
            return new InspectTargetResult(
                Found: false,
                Attachable: false,
                ProcessName: null,
                CommandLine: null,
                OperatingSystem: null,
                ProcessArchitecture: null,
                Warnings: Array.Empty<string>(),
                Error: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inspect target failed for PID {Pid}", pid);
            return new InspectTargetResult(
                Found: false,
                Attachable: false,
                ProcessName: null,
                CommandLine: null,
                OperatingSystem: null,
                ProcessArchitecture: null,
                Warnings: Array.Empty<string>(),
                Error: $"Inspect target failed: {ex.Message}"
            );
        }
    }
}

public record ListTargetsResult(
    RuntimeTargetInfo[] Targets,
    int Count
);

public record RuntimeTargetInfo(
    int Pid,
    string ProcessName
);

public record InspectTargetResult(
    bool Found,
    bool Attachable,
    string? ProcessName,
    string? CommandLine,
    string? OperatingSystem,
    string? ProcessArchitecture,
    string[] Warnings,
    string? Error
);
