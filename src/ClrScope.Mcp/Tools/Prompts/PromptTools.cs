using ModelContextProtocol.Server;

namespace ClrScope.Mcp.Tools.Prompts;

[McpServerToolType]
public sealed class PromptTools
{
    [McpServerTool(Name = "prompt_investigate_high_cpu")]
    public static string InvestigateHighCpu()
    {
        return @"Step-by-step guide for high CPU investigation:

1. Use runtime.list_targets to find the .NET process with high CPU usage
2. Use runtime.inspect_target to verify the process is .NET and get details
3. Use collect.trace with cpu-sampling profile for 30-60 seconds to capture CPU activity
4. Use collect.counters with System.Runtime provider to get CPU and thread metrics
5. Use collect.stacks to capture a snapshot of thread stacks
6. Open the trace in PerfView or dotnet-trace analyze to identify hot methods
7. Look for methods with high CPU time in the trace
8. Check thread pool configuration and contention in counters
9. Review stack traces to identify blocking patterns";
    }

    [McpServerTool(Name = "prompt_investigate_memory_leak")]
    public static string InvestigateMemoryLeak()
    {
        return @"Step-by-step guide for memory leak investigation:

1. Use runtime.list_targets to find the .NET process with high memory usage
2. Use runtime.inspect_target to verify the process is .NET and get details
3. Use collect.gcdump to capture GC heap snapshot
4. Use collect.counters with System.Runtime provider to get GC metrics
5. Use collect.trace with gc-heap profile to capture allocation activity
6. Open the gcdump in dotnet-gcdump or dotnet-gcdump-analyzer
7. Check heap size and generation distribution
8. Identify top types by size and count
9. Look for large object arrays or strings
10. Check GC pause times in counters
11. Review allocation rate in trace";
    }

    [McpServerTool(Name = "prompt_investigate_hang")]
    public static string InvestigateHang()
    {
        return @"Step-by-step guide for hang/deadlock investigation:

1. Use runtime.list_targets to find the .NET process that is hung
2. Use runtime.inspect_target to verify the process is .NET and get details
3. Use collect.dump to capture a full memory dump
4. Use collect.stacks to capture managed thread stacks
5. Use collect.counters with System.Runtime provider to get thread metrics
6. Use analyze_dump_sos with 'threads' command to list all threads
7. Use analyze_dump_sos with 'clrstack' command to get stack traces for each thread
8. Look for threads blocked on locks, monitors, or wait handles
9. Check for deadlock patterns (circular wait chains)
10. Review thread pool queue length and worker threads
11. Check for async/await deadlocks or thread pool starvation";
    }

    [McpServerTool(Name = "prompt_baseline_performance")]
    public static string BaselinePerformance()
    {
        return @"Plan for collecting baseline performance data:

1. Use runtime.list_targets to identify the target process
2. Use runtime.inspect_target to verify the process is .NET and get details
3. Use collect.counters with System.Runtime provider for 60 seconds to capture baseline metrics
4. Use collect.trace with default profile for 60 seconds to capture baseline trace
5. Use collect.gcdump to capture baseline GC heap snapshot
6. Use collect.stacks to capture baseline thread stacks
7. Save the collected artifacts with descriptive names (e.g., 'baseline_cpu.trace', 'baseline_counters.json')
8. Document the system conditions (CPU, memory, load) during baseline collection
9. Store baseline artifacts in a dedicated folder for comparison with future collections";
    }
}
