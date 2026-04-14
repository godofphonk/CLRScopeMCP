using ClrScope.Mcp.Contracts;
using Microsoft.Diagnostics.NETCore.Client;
using System.Runtime.InteropServices;

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

        // Get OS information from current system (best-effort, assumes same-host)
        var osDescription = RuntimeInformation.OSDescription;
        var osPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" :
                         RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                         RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Unknown";

        // Cannot reliably determine target process architecture from host
        // Return Unknown to avoid misleading information
        var architecture = "Unknown";

        // Note: CommandLine is not reliably available for external processes
        // and is omitted to avoid misleading information. This would require platform-specific inspection.

        var details = new RuntimeTargetDetails(
            pid,
            processName,
            CommandLine: null,
            OperatingSystem: $"{osPlatform} ({osDescription})",
            ProcessArchitecture: architecture
        );

        return InspectTargetResult.Success(details, warnings.ToArray());
    }
}
