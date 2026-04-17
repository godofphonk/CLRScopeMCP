using ClrScope.Mcp.Domain.Artifacts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Tools.Analysis;

internal sealed class ArtifactContentAnalyzer
{
    private readonly ILogger<ArtifactContentAnalyzer> _logger;

    public ArtifactContentAnalyzer(ILogger<ArtifactContentAnalyzer> logger)
    {
        _logger = logger;
    }

    public ArtifactAnalysisResult Analyze(Artifact artifact, string focus = "all")
    {
        try
        {
            var summary = new ArtifactAnalysisSummary(
                ArtifactId: artifact.ArtifactId.Value,
                Kind: artifact.Kind.ToString(),
                SizeBytes: artifact.SizeBytes,
                Status: artifact.Status.ToString(),
                Pid: artifact.Pid,
                CreatedAt: artifact.CreatedAtUtc,
                FilePath: artifact.FilePath,
                KeyMetrics: new Dictionary<string, string>(),
                Findings: new List<string>(),
                Recommendations: new List<string>()
            );

            // Add basic metrics
            summary.KeyMetrics["Size"] = FormatBytes(artifact.SizeBytes);
            summary.KeyMetrics["Status"] = artifact.Status.ToString();
            summary.KeyMetrics["Process ID"] = artifact.Pid.ToString();
            summary.KeyMetrics["Created"] = artifact.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss");
            summary.KeyMetrics["Analysis Focus"] = focus;

            // Analyze based on kind and focus
            switch (artifact.Kind)
            {
                case ArtifactKind.Stacks:
                    AnalyzeStacksArtifact(artifact, summary, focus);
                    break;
                case ArtifactKind.Dump:
                    AnalyzeDumpArtifact(artifact, summary, focus);
                    break;
                case ArtifactKind.GcDump:
                    AnalyzeGCDumpArtifact(artifact, summary, focus);
                    break;
                case ArtifactKind.Trace:
                    AnalyzeTraceArtifact(artifact, summary, focus);
                    break;
                case ArtifactKind.Counters:
                    AnalyzeCountersArtifact(artifact, summary, focus);
                    break;
                default:
                    summary.Findings.Add($"Artifact kind {artifact.Kind} analysis not implemented yet.");
                    break;
            }

            // Add general recommendations
            if (artifact.Status == ArtifactStatus.Completed)
            {
                summary.Recommendations.Add("Artifact collection completed successfully.");
                summary.Recommendations.Add("Use appropriate analysis tools to examine the content.");
            }
            else if (artifact.Status == ArtifactStatus.Failed)
            {
                summary.Recommendations.Add("Artifact collection failed. Check error logs.");
                summary.Recommendations.Add("Retry collection if the issue is transient.");
            }

            return ArtifactAnalysisResult.Success(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze artifact content");
            return ArtifactAnalysisResult.Failure($"Failed to analyze artifact content: {ex.Message}");
        }
    }

    private void AnalyzeStacksArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Stacks artifact contains managed thread stack traces.");

        try
        {
            if (File.Exists(artifact.FilePath))
            {
                var fileContent = File.ReadAllText(artifact.FilePath);
                var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var threadCount = ExtractThreadCount(lines);
                var blockedThreads = CountBlockedThreads(lines);
                var runningThreads = CountRunningThreads(lines);

                summary.KeyMetrics["Thread Count"] = threadCount.ToString();
                summary.KeyMetrics["Blocked Threads"] = blockedThreads.ToString();
                summary.KeyMetrics["Running Threads"] = runningThreads.ToString();

                if (blockedThreads > 0)
                {
                    summary.Findings.Add($"{blockedThreads} threads appear blocked (potential deadlock or contention).");
                }

                if (runningThreads > 5)
                {
                    summary.Findings.Add($"{runningThreads} threads in running state (potential CPU hotspot).");
                }

                if (focus == "threads" || focus == "all")
                {
                    if (blockedThreads > 0)
                    {
                        summary.Recommendations.Add($"Investigate {blockedThreads} blocked threads for circular wait patterns.");
                    }
                    summary.Recommendations.Add("Use `analyze_dump_sos` with 'threads' and 'clrstack' commands for detailed analysis.");
                }

                if (focus == "cpu" || focus == "all" && runningThreads > 0)
                {
                    summary.Recommendations.Add($"Focus on {runningThreads} running threads for CPU hotspots.");
                    summary.Recommendations.Add("Use flame graph visualization to identify hot methods.");
                }
            }
            else
            {
                summary.KeyMetrics["Thread Count"] = "File not found";
            }
        }
        catch (Exception)
        {
            summary.KeyMetrics["Thread Count"] = "Analysis failed";
        }

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Check stack frames for memory allocation patterns.");
        }

        if (focus == "io" || focus == "all")
        {
            summary.Recommendations.Add("Look for I/O operations blocking threads.");
        }
    }

    private void AnalyzeDumpArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Memory dump contains full process memory snapshot.");
        summary.KeyMetrics["Dump Size"] = FormatBytes(artifact.SizeBytes);

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Use `analyze_dump_sos` with 'dumpheap -stat' to check for large objects.");
            summary.Recommendations.Add("Check GC heap state with 'gcheap' command.");
            summary.Recommendations.Add("Look for memory leaks by examining gen2 and LOH.");
        }

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Use `analyze_dump_sos` with 'threads' and 'clrstack' commands.");
            summary.Recommendations.Add("Examine thread stacks for blocking patterns.");
        }

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Check thread CPU usage and thread pool state.");
        }

        if (focus == "io" || focus == "all")
        {
            summary.Recommendations.Add("Look for I/O operations in thread stacks.");
        }
    }

    private void AnalyzeGCDumpArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("GC dump contains heap snapshot with object graph.");
        summary.KeyMetrics["Heap Snapshot"] = "Available";

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Use dotnet-gcdump or dotnet-gcdump-analyzer to examine heap.");
            summary.Recommendations.Add("Check for large object arrays and strings.");
            summary.Recommendations.Add("Identify top types by size and count.");
            summary.Recommendations.Add("Examine gen2 and LOH for memory leaks.");
        }

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Check for excessive allocation rates.");
        }

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Examine object references to identify thread-local allocations.");
        }
    }

    private void AnalyzeTraceArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Trace contains EventPipe events for performance analysis.");
        summary.KeyMetrics["Trace Size"] = FormatBytes(artifact.SizeBytes);

        // .nettrace is a binary format - cannot use ReadAllText
        // This requires TraceEvent library or dotnet-trace convert for proper parsing
        summary.Findings.Add("Binary trace format requires TraceEvent library or dotnet-trace convert for detailed analysis.");
        summary.Recommendations.Add("Use dotnet-trace convert --format Speedscope to convert to a readable format for analysis.");
    }

    private void AnalyzeCountersArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Counters contain performance metrics over time.");
        summary.KeyMetrics["Metrics"] = "Performance counters";

        try
        {
            if (File.Exists(artifact.FilePath))
            {
                var fileContent = File.ReadAllText(artifact.FilePath);

                var hasCpuMetrics = fileContent.Contains("cpu") || fileContent.Contains("processor") || fileContent.Contains("% Processor Time");
                var hasMemoryMetrics = fileContent.Contains("memory") || fileContent.Contains("gc") || fileContent.Contains("working-set");
                var hasThreadPoolMetrics = fileContent.Contains("threadpool") || fileContent.Contains("worker") || fileContent.Contains("io");
                var hasExceptionMetrics = fileContent.Contains("exception") || fileContent.Contains("error");

                summary.KeyMetrics["CPU Metrics"] = hasCpuMetrics ? "Yes" : "No";
                summary.KeyMetrics["Memory Metrics"] = hasMemoryMetrics ? "Yes" : "No";
                summary.KeyMetrics["ThreadPool Metrics"] = hasThreadPoolMetrics ? "Yes" : "No";
                summary.KeyMetrics["Exception Metrics"] = hasExceptionMetrics ? "Yes" : "No";

                if (hasCpuMetrics)
                {
                    summary.Findings.Add("Counters contain CPU usage metrics for performance analysis.");
                }

                if (hasMemoryMetrics)
                {
                    summary.Findings.Add("Counters contain memory/GC metrics for leak detection.");
                }

                if (hasThreadPoolMetrics)
                {
                    summary.Findings.Add("Counters contain thread pool metrics for starvation analysis.");
                }

                if (hasExceptionMetrics)
                {
                    summary.Findings.Add("Counters contain exception metrics for error analysis.");
                }

                if (focus == "cpu" || focus == "all")
                {
                    if (hasCpuMetrics)
                    {
                        summary.Recommendations.Add("Examine CPU usage and processor time metrics.");
                        summary.Recommendations.Add("Check for CPU spikes and sustained high usage.");
                    }
                    else
                    {
                        summary.Recommendations.Add("Consider adding System.Runtime CPU counters for CPU analysis.");
                    }
                }

                if (focus == "memory" || focus == "all")
                {
                    if (hasMemoryMetrics)
                    {
                        summary.Recommendations.Add("Analyze memory allocation and GC metrics.");
                        summary.Recommendations.Add("Check for memory leaks and high GC pressure.");
                    }
                    else
                    {
                        summary.Recommendations.Add("Consider adding System.Runtime GC counters for memory analysis.");
                    }
                }

                if (focus == "threads" || focus == "all")
                {
                    if (hasThreadPoolMetrics)
                    {
                        summary.Recommendations.Add("Examine thread pool usage and contention metrics.");
                        summary.Recommendations.Add("Check for thread pool starvation or excessive thread creation.");
                    }
                }

                if (focus == "io" || focus == "all")
                {
                    summary.Recommendations.Add("Look for I/O and network-related counters.");
                }

                if (focus == "all")
                {
                    summary.Recommendations.Add("Compare with baseline to identify anomalies.");
                }
            }
            else
            {
                summary.KeyMetrics["Analysis"] = "File not found";
            }
        }
        catch (Exception)
        {
            summary.KeyMetrics["Analysis"] = "Analysis failed";
        }
    }

    private static int ExtractThreadCount(string[] lines)
    {
        var threadIds = new HashSet<int>();
        foreach (var line in lines)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"Thread (\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var threadId))
            {
                threadIds.Add(threadId);
            }
        }
        return threadIds.Count;
    }

    private static int CountBlockedThreads(string[] lines)
    {
        return lines.Count(line => line.Contains("Blocked") || line.Contains("Wait") || line.Contains("Lock"));
    }

    private static int CountRunningThreads(string[] lines)
    {
        return lines.Count(line => line.Contains("Running") || line.Contains("Preemptive") || line.Contains("CPU"));
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

public record ArtifactAnalysisResult(
    bool IsSuccess,
    ArtifactAnalysisSummary? Summary,
    string? Error)
{
    public static ArtifactAnalysisResult Success(ArtifactAnalysisSummary summary) =>
        new(true, summary, null);

    public static ArtifactAnalysisResult Failure(string error) =>
        new(false, null, error);
}

public record ArtifactAnalysisSummary(
    string ArtifactId,
    string Kind,
    long SizeBytes,
    string Status,
    int Pid,
    DateTime CreatedAt,
    string FilePath,
    Dictionary<string, string> KeyMetrics,
    List<string> Findings,
    List<string> Recommendations
);
