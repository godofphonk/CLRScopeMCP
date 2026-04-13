using Microsoft.Diagnostics.NETCore.Client;

namespace ClrScope.Mcp.Services;

public record RuntimeTarget(int Pid, string ProcessName);

public class RuntimeService
{
    public IReadOnlyList<RuntimeTarget> ListTargets()
    {
        var pids = DiagnosticsClient.GetPublishedProcesses();
        var targets = new List<RuntimeTarget>();

        foreach (var pid in pids)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(pid);
                targets.Add(new RuntimeTarget(pid, process.ProcessName));
            }
            catch (ArgumentException)
            {
                // Process exited between GetPublishedProcesses and GetProcessById
                // Skip this PID
                continue;
            }
        }

        return targets;
    }
}
