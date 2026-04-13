using ClrScope.Mcp.Contracts;
using Microsoft.Diagnostics.NETCore.Client;

namespace ClrScope.Mcp.Services;

public class InspectTargetService
{
    public InspectTargetResult InspectTarget(int pid)
    {
        var warnings = new List<string>();

        // Check if process exists
        System.Diagnostics.Process? process = null;
        try
        {
            process = System.Diagnostics.Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return InspectTargetResult.NotFound($"Process with PID {pid} not found");
        }

        // Check if process is .NET (attachable)
        var publishedProcesses = DiagnosticsClient.GetPublishedProcesses();
        var isAttachable = publishedProcesses.Contains(pid);

        if (!isAttachable)
        {
            return InspectTargetResult.NotAttachable(
                $"Process {pid} ({process.ProcessName}) is not a .NET process or not attachable",
                Array.Empty<string>()
            );
        }

        // Get process name (guaranteed)
        var processName = process.ProcessName;

        // Command line (best-effort - not available for external processes)
        string? commandLine = null;
        try
        {
            commandLine = process.StartInfo?.Arguments;
            if (string.IsNullOrEmpty(commandLine))
            {
                warnings.Add("commandLine is not available for external processes");
            }
        }
        catch
        {
            warnings.Add("commandLine is not available for external processes");
        }

        // Operating system (host-derived)
        var operatingSystem = Environment.OSVersion.Platform.ToString();

        // Process architecture (host-derived)
        var processArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";

        var details = new RuntimeTargetDetails(
            pid,
            processName,
            commandLine,
            operatingSystem,
            processArchitecture
        );

        return InspectTargetResult.Success(details, warnings.ToArray());
    }
}
