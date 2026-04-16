using Microsoft.Diagnostics.NETCore.Client;

namespace ClrScope.Mcp.Services.Runtime;

public record RuntimeTarget(int Pid, string ProcessName);

public class RuntimeService
{
    public IReadOnlyList<RuntimeTarget> ListTargets(string? processNameFilter = null, string? sortBy = null)
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

        // Apply filter if provided
        if (!string.IsNullOrEmpty(processNameFilter))
        {
            targets = targets
                .Where(t => t.ProcessName.Contains(processNameFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Apply sorting if provided
        if (!string.IsNullOrEmpty(sortBy))
        {
            switch (sortBy.ToLowerInvariant())
            {
                case "pid":
                    targets = targets.OrderBy(t => t.Pid).ToList();
                    break;
                case "name":
                    targets = targets.OrderBy(t => t.ProcessName).ToList();
                    break;
                case "pid_desc":
                    targets = targets.OrderByDescending(t => t.Pid).ToList();
                    break;
                case "name_desc":
                    targets = targets.OrderByDescending(t => t.ProcessName).ToList();
                    break;
            }
        }

        return targets;
    }
}
