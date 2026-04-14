using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Workflows;

[McpServerToolType]
public sealed class WorkflowTools
{
    [McpServerTool(Name = "workflow_capture_high_cpu_bundle"), Description("Get step-by-step instructions for collecting high CPU diagnostic bundle")]
    public static string CaptureHighCpuBundle(int pid)
    {
        return @"High CPU Bundle Collection Workflow

Follow these steps to collect a high CPU diagnostic bundle for PID " + pid + @":

1. Collect CPU trace:
   - Use: collect_trace with pid=" + pid + @" duration=00:01:00
   - This captures CPU sampling data to identify hot methods

2. Collect performance counters:
   - Use: collect_counters with pid=" + pid + @" providers=[System.Runtime] duration=00:01:00
   - This captures CPU and thread metrics over time

3. Collect thread stacks:
   - Use: collect_stacks with pid=" + pid + @"
   - This captures a snapshot of thread stacks

4. After collection, analyze the results:
   - Use artifact_summarize with the artifact IDs
   - Use session_analyze with the session ID

Note: Execute these commands in sequence. The collection may take 1-2 minutes.";
    }

    [McpServerTool(Name = "workflow_capture_memory_leak_bundle"), Description("Get step-by-step instructions for collecting memory leak diagnostic bundle")]
    public static string CaptureMemoryLeakBundle(int pid)
    {
        return @"Memory Leak Bundle Collection Workflow

Follow these steps to collect a memory leak diagnostic bundle for PID " + pid + @":

1. Collect GC dump:
   - Use: collect_gcdump with pid=" + pid + @"
   - This captures the GC heap snapshot

2. Collect GC counters:
   - Use: collect_counters with pid=" + pid + @" providers=[System.Runtime] duration=00:01:00
   - This captures GC metrics over time

3. Collect GC heap trace:
   - Use: collect_trace with pid=" + pid + @" duration=00:01:00
   - This captures allocation activity

4. After collection, analyze the results:
   - Use artifact_summarize with the artifact IDs
   - Use session_analyze with the session ID

Note: Execute these commands in sequence. The collection may take 1-2 minutes.";
    }

    [McpServerTool(Name = "workflow_capture_hang_bundle"), Description("Get step-by-step instructions for collecting hang/deadlock diagnostic bundle")]
    public static string CaptureHangBundle(int pid)
    {
        return @"Hang/Deadlock Bundle Collection Workflow

Follow these steps to collect a hang diagnostic bundle for PID " + pid + @":

1. Collect memory dump:
   - Use: collect_dump with pid=" + pid + @"
   - This captures a full process memory dump

2. Collect thread stacks:
   - Use: collect_stacks with pid=" + pid + @"
   - This captures managed thread stacks

3. Collect thread counters:
   - Use: collect_counters with pid=" + pid + @" providers=[System.Runtime] duration=00:00:30
   - This captures thread metrics

4. After collection, analyze the results:
   - Use analyze_dump_sos with the dump artifact
   - Use session_analyze with the session ID

Note: Execute these commands in sequence. The collection may take 1-2 minutes.";
    }

    [McpServerTool(Name = "workflow_capture_baseline_bundle"), Description("Get step-by-step instructions for collecting baseline performance bundle")]
    public static string CaptureBaselineBundle(int pid)
    {
        return @"Baseline Performance Bundle Collection Workflow

Follow these steps to collect a baseline performance bundle for PID " + pid + @":

1. Collect performance counters:
   - Use: collect_counters with pid=" + pid + @" providers=[System.Runtime] duration=00:01:00
   - This captures baseline metrics

2. Collect baseline trace:
   - Use: collect_trace with pid=" + pid + @" duration=00:01:00
   - This captures baseline CPU activity

3. Collect GC dump:
   - Use: collect_gcdump with pid=" + pid + @"
   - This captures baseline GC heap state

4. Collect thread stacks:
   - Use: collect_stacks with pid=" + pid + @"
   - This captures baseline thread stacks

5. After collection, analyze the results:
   - Use artifact_summarize with the artifact IDs
   - Use session_analyze with the session ID

Note: Execute these commands in sequence. The collection may take 2-3 minutes.";
    }
}
