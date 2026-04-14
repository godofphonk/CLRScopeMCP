using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Analysis;

[McpServerToolType]
public sealed class SummaryTools
{
    [McpServerTool(Name = "artifact_summarize"), Description("Analyze and summarize an artifact with findings and recommendations. Focus options: all, memory, threads, cpu, io")]
    public static async Task<ArtifactAnalysisResult> SummarizeArtifact(
        string artifactId,
        McpServer server,
        [Description("Analysis focus: 'all' (default), 'memory', 'threads', 'cpu', 'io'")] string focus = "all",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return ArtifactAnalysisResult.Failure($"Artifact not found: {artifactId}");
            }

            // Analyze the artifact based on its kind and focus
            var analysis = AnalyzeArtifactContent(artifact, logger, focus.ToLowerInvariant());
            return analysis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to summarize artifact: {ArtifactId}", artifactId);
            return ArtifactAnalysisResult.Failure($"Failed to summarize artifact: {ex.Message}");
        }
    }

    [McpServerTool(Name = "detect_patterns"), Description("Automatically detect common problem patterns (memory leaks, deadlocks, thread pool starvation, high CPU)")]
    public static async Task<PatternDetectionResult> DetectPatterns(
        string artifactId,
        McpServer server,
        [Description("Pattern types to detect: 'all' (default), 'memory_leaks', 'deadlocks', 'thread_pool', 'high_cpu', 'excessive_allocations'")] string patternTypes = "all",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var sosAnalyzer = server.Services!.GetRequiredService<ISosAnalyzer>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();
        var options = server.Services!.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClrScopeOptions>>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return PatternDetectionResult.Failure($"Artifact not found: {artifactId}");
            }

            // Validate artifact path is within artifact root
            var artifactRoot = options.Value.GetArtifactRoot();
            PathSecurity.EnsurePathWithinDirectory(artifact.FilePath, artifactRoot);

            // Only dump and gcdump artifacts support pattern detection
            if (artifact.Kind != ArtifactKind.Dump && artifact.Kind != ArtifactKind.GcDump)
            {
                return PatternDetectionResult.Failure($"Pattern detection only supports Dump and GcDump artifacts, got: {artifact.Kind}");
            }

            // Check if dotnet-dump is available
            if (!await sosAnalyzer.IsAvailableAsync(cancellationToken))
            {
                return PatternDetectionResult.Failure("dotnet-dump CLI not found. Pattern detection requires SOS commands.");
            }

            var detectedPatterns = new List<DetectedPattern>();
            var patternsToDetect = patternTypes.ToLowerInvariant() == "all"
                ? new[] { "memory_leaks", "deadlocks", "thread_pool", "high_cpu", "excessive_allocations" }
                : patternTypes.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Detect patterns based on artifact kind
            if (artifact.Kind == ArtifactKind.Dump)
            {
                await DetectDumpPatterns(artifact.FilePath, sosAnalyzer, patternsToDetect, detectedPatterns, cancellationToken);
            }
            else if (artifact.Kind == ArtifactKind.GcDump)
            {
                await DetectGCDumpPatterns(artifact.FilePath, sosAnalyzer, patternsToDetect, detectedPatterns, cancellationToken);
            }

            return PatternDetectionResult.Success(detectedPatterns.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pattern detection failed for artifact {ArtifactId}", artifactId);
            return PatternDetectionResult.Failure($"Pattern detection failed: {ex.Message}");
        }
    }

    private static async Task DetectDumpPatterns(string filePath, ISosAnalyzer sosAnalyzer, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        foreach (var pattern in patternsToDetect)
        {
            switch (pattern)
            {
                case "memory_leaks":
                    await DetectMemoryLeaks(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "deadlocks":
                    await DetectDeadlocks(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "thread_pool":
                    await DetectThreadPoolIssues(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "high_cpu":
                    await DetectHighCPU(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "excessive_allocations":
                    await DetectExcessiveAllocations(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
            }
        }
    }

    private static async Task DetectGCDumpPatterns(string filePath, ISosAnalyzer sosAnalyzer, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        foreach (var pattern in patternsToDetect)
        {
            switch (pattern)
            {
                case "memory_leaks":
                    await DetectGCDumpMemoryLeaks(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "excessive_allocations":
                    await DetectGCDumpAllocations(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
            }
        }
    }

    private static async Task DetectMemoryLeaks(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "dumpheap -stat", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        // Analyze for memory leak indicators
        if (output.Contains("Large Object Heap") && output.Contains("MB"))
        {
            var lohSize = ExtractSizeFromOutput(output, "Large Object Heap");
            if (lohSize > 100 * 1024 * 1024) // > 100MB
            {
                detectedPatterns.Add(new DetectedPattern(
                    PatternType: "memory_leak",
                    Severity: "high",
                    Description: $"Large Object Heap size is {FormatBytes(lohSize)}, indicating potential memory leak",
                    Recommendation: "Investigate large objects in LOH using 'dumpheap -mt <MethodTable>' and check for pinned objects"
                ));
            }
        }

        // Check for gen2 growth
        if (output.Contains("Gen 2"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "medium",
                Description: "Gen 2 heap statistics available - review for long-lived objects",
                Recommendation: "Use 'dumpheap -stat' and 'gcroot' to identify roots of long-lived objects"
            ));
        }
    }

    private static async Task DetectDeadlocks(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "threads", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        var threadLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var blockedThreads = 0;

        foreach (var line in threadLines)
        {
            if (line.Contains("Blocked") || line.Contains("Wait"))
            {
                blockedThreads++;
            }
        }

        if (blockedThreads > 5)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "deadlock",
                Severity: "high",
                Description: $"{blockedThreads} threads appear blocked, potential deadlock",
                Recommendation: "Use 'clrstack' on blocked threads to identify circular wait patterns"
            ));
        }
    }

    private static async Task DetectThreadPoolIssues(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "threadpool", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        if (output.Contains("Work") && output.Contains("min") && output.Contains("max"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "medium",
                Description: "Thread pool statistics available - review for starvation",
                Recommendation: "Check thread pool queue length and worker thread utilization"
            ));
        }
    }

    private static async Task DetectHighCPU(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "threads", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        if (output.Contains("Running") || output.Contains("Preemptive"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "medium",
                Description: "Threads in running state detected - review for CPU hotspots",
                Recommendation: "Use 'clrstack' on running threads to identify CPU-intensive methods"
            ));
        }
    }

    private static async Task DetectExcessiveAllocations(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "dumpheap -stat", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        // Look for large allocation counts
        if (output.Contains("Count") && output.Contains("Total"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "excessive_allocations",
                Severity: "medium",
                Description: "Heap statistics available - review allocation patterns",
                Recommendation: "Check top types by count to identify excessive allocations"
            ));
        }
    }

    private static async Task DetectGCDumpMemoryLeaks(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        detectedPatterns.Add(new DetectedPattern(
            PatternType: "memory_leak",
            Severity: "medium",
            Description: "GC dump analysis for memory leaks requires dotnet-gcdump-analyzer",
            Recommendation: "Use dotnet-gcdump-analyze to examine heap growth and object references"
        ));
    }

    private static async Task DetectGCDumpAllocations(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        detectedPatterns.Add(new DetectedPattern(
            PatternType: "excessive_allocations",
            Severity: "medium",
            Description: "GC dump allocation analysis requires dotnet-gcdump-analyzer",
            Recommendation: "Use dotnet-gcdump-analyze to examine allocation rates and patterns"
        ));
    }

    private static long ExtractSizeFromOutput(string output, string keyword)
    {
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains(keyword))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[^1], out var size))
                {
                    return size;
                }
            }
        }
        return 0;
    }

    private static ArtifactAnalysisResult AnalyzeArtifactContent(Artifact artifact, ILogger logger, string focus = "all")
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
            logger.LogError(ex, "Failed to analyze artifact content");
            return ArtifactAnalysisResult.Failure($"Failed to analyze artifact content: {ex.Message}");
        }
    }

    private static void AnalyzeStacksArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Stacks artifact contains managed thread stack traces.");
        summary.KeyMetrics["Thread Count"] = "Unknown (analyze file for details)";

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Use `analyze_dump_sos` with 'threads' and 'clrstack' commands to analyze.");
            summary.Recommendations.Add("Look for threads blocked on locks, monitors, or synchronization primitives.");
        }

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Check stack frames for memory allocation patterns.");
        }

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Identify hot methods appearing frequently across thread stacks.");
        }

        if (focus == "io" || focus == "all")
        {
            summary.Recommendations.Add("Look for I/O operations blocking threads.");
        }
    }

    private static void AnalyzeDumpArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
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

    private static void AnalyzeGCDumpArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
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

    private static void AnalyzeTraceArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Trace contains EventPipe events for performance analysis.");
        summary.KeyMetrics["Trace Size"] = FormatBytes(artifact.SizeBytes);

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Use PerfView or dotnet-trace analyze to examine CPU samples.");
            summary.Recommendations.Add("Look for hot methods with high CPU time.");
        }

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Check for allocation patterns and GC activity.");
            summary.Recommendations.Add("Examine GC heap allocation rate and pause times.");
        }

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Analyze thread pool usage and contention.");
        }

        if (focus == "io" || focus == "all")
        {
            summary.Recommendations.Add("Look for I/O operations and network activity.");
        }
    }

    private static void AnalyzeCountersArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Counters contain performance metrics over time.");
        summary.KeyMetrics["Metrics"] = "Performance counters";

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Examine CPU usage and processor time metrics.");
            summary.Recommendations.Add("Check for CPU spikes and sustained high usage.");
        }

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Analyze memory allocation and GC metrics.");
            summary.Recommendations.Add("Check for memory leaks and high GC pressure.");
        }

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Examine thread pool usage and contention metrics.");
            summary.Recommendations.Add("Check for thread pool starvation or excessive thread creation.");
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
    string? Error
)
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

public record PatternDetectionResult(
    bool IsSuccess,
    DetectedPattern[] Patterns,
    string? Error
)
{
    public static PatternDetectionResult Success(DetectedPattern[] patterns) =>
        new(true, patterns, null);

    public static PatternDetectionResult Failure(string error) =>
        new(false, Array.Empty<DetectedPattern>(), error);
}

public record DetectedPattern(
    string PatternType,
    string Severity,
    string Description,
    string Recommendation
);
