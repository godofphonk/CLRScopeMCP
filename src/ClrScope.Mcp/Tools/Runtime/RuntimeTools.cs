using ClrScope.Mcp.Services.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Runtime;

[McpServerToolType]
public sealed class RuntimeTools
{
    [McpServerTool(Name = "runtime_list_targets", Title = "List .NET Processes", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("List all attachable .NET processes via GetPublishedProcesses(). Supports filtering by process name and sorting by pid or name.")]
    public static ListTargetsResult ListTargets(
        McpServer server,
        CancellationToken cancellationToken = default,
        [Description("Filter processes by name (case-insensitive substring match)")] string? processNameFilter = null,
        [Description("Sort by: 'pid', 'name', 'pid_desc', 'name_desc'")] string? sortBy = null)
    {
        // Validate sortBy before getting services
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var validSortOptions = new[] { "pid", "name", "pid_desc", "name_desc" };
            if (!validSortOptions.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            {
                return new ListTargetsResult(
                    Targets: Array.Empty<RuntimeTargetInfo>(),
                    Count: 0,
                    Error: $"SortBy must be one of: {string.Join(", ", validSortOptions)}"
                );
            }
        }

        var runtimeService = server.Services!.GetRequiredService<RuntimeService>();
        var logger = server.Services!.GetRequiredService<ILogger<RuntimeTools>>();

        try
        {
            var targets = runtimeService.ListTargets(processNameFilter, sortBy);
            logger.LogInformation("Found {Count} .NET processes", targets.Count);

            return new ListTargetsResult(
                Targets: targets.Select(t => new RuntimeTargetInfo(t.Pid, t.ProcessName)).ToArray(),
                Count: targets.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "List targets failed");
            return new ListTargetsResult(
                Targets: Array.Empty<RuntimeTargetInfo>(),
                Count: 0,
                Error: $"List targets failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "runtime_inspect_target", Title = "Inspect .NET Process", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Get detailed information about a .NET process. Returns guaranteed fields (pid, processName, isAttachable) and best-effort fields (commandLine, OS, architecture).")]
    public static InspectTargetResult InspectTarget(
        [Description("Process ID to inspect")] int pid,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        // Validate PID before getting services
        if (pid <= 0)
        {
            return new InspectTargetResult(
                Found: false,
                Attachable: false,
                ProcessName: null,
                CommandLine: null,
                OperatingSystem: null,
                ProcessArchitecture: null,
                Warnings: Array.Empty<string>(),
                Error: "Process ID must be greater than 0"
            );
        }

        var inspectService = server.Services!.GetRequiredService<InspectTargetService>();
        var logger = server.Services!.GetRequiredService<ILogger<RuntimeTools>>();

        try
        {
            var result = inspectService.InspectTarget(pid);
            
            if (!result.Found)
            {
                logger.LogWarning("Process {Pid} not found", pid);
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

            logger.LogInformation("Inspected process {Pid}: Found={Found}, Attachable={Attachable}", pid, result.Found, result.Attachable);

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
            logger.LogError(ex, "Invalid input for target inspection: {Message}", ex.Message);
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
            logger.LogError(ex, "Inspect target failed for {Pid}", pid);
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
    int Count,
    string Error = ""
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
