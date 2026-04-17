using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Domain.Heap.Enums;
using ClrScope.Mcp.Domain.Heap.Options;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClrScope.Mcp.Tools.Analysis;

// Layer 1: Artifact Inspection - determines artifact type and properties
internal enum AnalysisPhase
{
    Validating,
    PreparingSymbols,
    RunningAnalysis,
    ParsingStacks,
    Aggregating,
    Rendering,
    Completed
}

internal record AnalysisProgress(
    AnalysisPhase Phase,
    int CurrentStep,
    int TotalSteps,
    string Message
);

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

            var detectedPatterns = new List<DetectedPattern>();
            var patternsToDetect = patternTypes.ToLowerInvariant() == "all"
                ? new[] { "memory_leaks", "deadlocks", "thread_pool", "high_cpu", "excessive_allocations" }
                : patternTypes.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Detect patterns based on artifact kind
            switch (artifact.Kind)
            {
                case ArtifactKind.Dump:
                    // Check if dotnet-dump is available
                    if (!await sosAnalyzer.IsAvailableAsync(cancellationToken))
                    {
                        return PatternDetectionResult.Failure("dotnet-dump CLI not found. Pattern detection for Dump requires SOS commands.");
                    }
                    await DetectDumpPatterns(artifact.FilePath, sosAnalyzer, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                case ArtifactKind.GcDump:
                    // Check if dotnet-dump is available
                    if (!await sosAnalyzer.IsAvailableAsync(cancellationToken))
                    {
                        return PatternDetectionResult.Failure("dotnet-dump CLI not found. Pattern detection for GcDump requires SOS commands.");
                    }
                    await DetectGCDumpPatterns(artifact.FilePath, sosAnalyzer, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                case ArtifactKind.Trace:
                    await DetectTracePatterns(artifact.FilePath, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                case ArtifactKind.Counters:
                    await DetectCountersPatterns(artifact.FilePath, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                case ArtifactKind.Stacks:
                    await DetectStacksPatterns(artifact.FilePath, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                default:
                    return PatternDetectionResult.Failure($"Pattern detection not supported for artifact kind: {artifact.Kind}");
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
            Recommendation: "Use dotnet-gcdump-analyzer to examine allocation rates and patterns"
        ));
    }

    private static async Task DetectTracePatterns(string filePath, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            // .nettrace is a binary format - cannot use ReadAllTextAsync
            // This requires TraceEvent library or dotnet-trace convert for proper parsing
            // Pattern detection not available for binary trace files
            return;
        }
        catch (Exception)
        {
            // If trace file cannot be read, skip pattern detection
        }
    }

    private static void DetectTraceHighCPU(string fileContent, long traceSize, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("CPU") || fileContent.Contains("Sample") || fileContent.Contains("cpu-sampling"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "medium",
                Description: "Trace contains CPU sampling data - analyze for CPU hotspots",
                Recommendation: "Use PerfView or dotnet-trace analyze to identify hot methods with high CPU time"
            ));
        }

        if (traceSize > 10 * 1024 * 1024)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "low",
                Description: $"Large trace file ({FormatBytes(traceSize)}) - may indicate long CPU-intensive period",
                Recommendation: "Review trace duration and focus on period of high CPU activity"
            ));
        }
    }

    private static void DetectTraceMemoryLeaks(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("GC") || fileContent.Contains("Heap") || fileContent.Contains("gc-heap"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "medium",
                Description: "Trace contains GC/heap events - analyze for memory growth patterns",
                Recommendation: "Check GC pause times, heap size growth, and allocation rates over time"
            ));
        }

        if (fileContent.Contains("Gen2") || fileContent.Contains("LOH") || fileContent.Contains("Large Object"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "high",
                Description: "Trace contains Gen2/LOH activity - potential memory leak indicators",
                Recommendation: "Examine long-lived objects in Gen2 and Large Object Heap for leak sources"
            ));
        }
    }

    private static void DetectTraceThreadPool(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("ThreadPool") || fileContent.Contains("Worker") || fileContent.Contains("IO"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "medium",
                Description: "Trace contains thread pool events - analyze for starvation or contention",
                Recommendation: "Check thread pool queue length, worker thread utilization, and I/O completion port usage"
            ));
        }

        if (fileContent.Contains("Starvation") || fileContent.Contains("Exhaustion") || fileContent.Contains("Queue"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "high",
                Description: "Trace indicates potential thread pool starvation or queue buildup",
                Recommendation: "Review thread pool configuration and adjust min/max threads if needed"
            ));
        }
    }

    private static void DetectTraceAllocations(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("Alloc") || fileContent.Contains("Allocation"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "excessive_allocations",
                Severity: "medium",
                Description: "Trace contains allocation events - analyze for allocation hotspots",
                Recommendation: "Identify methods with high allocation rates and consider object pooling or reuse"
            ));
        }
    }

    private static async Task DetectCountersPatterns(string filePath, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);

            foreach (var pattern in patternsToDetect)
            {
                switch (pattern)
                {
                    case "high_cpu":
                        DetectCountersHighCPU(fileContent, detectedPatterns);
                        break;
                    case "memory_leaks":
                        DetectCountersMemoryLeaks(fileContent, detectedPatterns);
                        break;
                    case "thread_pool":
                        DetectCountersThreadPool(fileContent, detectedPatterns);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // If counters file cannot be read, skip pattern detection
        }
    }

    private static void DetectCountersHighCPU(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("cpu-usage") || fileContent.Contains("process-cpu") || fileContent.Contains("% Processor Time"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "medium",
                Description: "Counters contain CPU usage metrics - analyze for high CPU periods",
                Recommendation: "Review CPU usage over time and identify periods of sustained high CPU"
            ));
        }
    }

    private static void DetectCountersMemoryLeaks(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("memory") || fileContent.Contains("gc-heap") || fileContent.Contains("working-set"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "medium",
                Description: "Counters contain memory metrics - analyze for memory growth patterns",
                Recommendation: "Check for steady memory growth over time indicating potential memory leak"
            ));
        }

        if (fileContent.Contains("gen-2") || fileContent.Contains("loh"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "high",
                Description: "Counters show Gen2/LOH activity - potential memory leak indicators",
                Recommendation: "Monitor Gen2 and LOH size over time for steady growth patterns"
            ));
        }
    }

    private static void DetectCountersThreadPool(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("threadpool") || fileContent.Contains("worker-threads") || fileContent.Contains("io-threads"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "medium",
                Description: "Counters contain thread pool metrics - analyze for starvation",
                Recommendation: "Check thread pool queue length and thread utilization for signs of starvation"
            ));
        }
    }

    private static async Task DetectStacksPatterns(string filePath, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pattern in patternsToDetect)
            {
                switch (pattern)
                {
                    case "deadlocks":
                        DetectStacksDeadlocks(lines, detectedPatterns);
                        break;
                    case "thread_pool":
                        DetectStacksThreadPool(lines, detectedPatterns);
                        break;
                    case "high_cpu":
                        DetectStacksHighCPU(lines, detectedPatterns);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // If stacks file cannot be read, skip pattern detection
        }
    }

    private static void DetectStacksDeadlocks(string[] lines, List<DetectedPattern> detectedPatterns)
    {
        var blockedThreads = 0;
        var threadIds = new HashSet<int>();

        foreach (var line in lines)
        {
            if (line.Contains("Blocked") || line.Contains("Wait") || line.Contains("Lock"))
            {
                blockedThreads++;
            }

            var match = System.Text.RegularExpressions.Regex.Match(line, @"Thread (\d+)");
            if (match.Success)
            {
                threadIds.Add(int.Parse(match.Groups[1].Value));
            }
        }

        if (blockedThreads > 5)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "deadlock",
                Severity: "high",
                Description: $"{blockedThreads} threads appear blocked, potential deadlock",
                Recommendation: "Examine stack frames of blocked threads to identify circular wait patterns and lock contention"
            ));
        }

        if (threadIds.Count > 0)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "deadlock",
                Severity: "low",
                Description: $"Stacks contain {threadIds.Count} threads - review for blocking patterns",
                Recommendation: "Check thread states and identify threads waiting on synchronization primitives"
            ));
        }
    }

    private static void DetectStacksThreadPool(string[] lines, List<DetectedPattern> detectedPatterns)
    {
        var threadPoolThreads = 0;

        foreach (var line in lines)
        {
            if (line.Contains("ThreadPool") || line.Contains("Worker Thread") || line.Contains(".NET ThreadPool"))
            {
                threadPoolThreads++;
            }
        }

        if (threadPoolThreads > 0)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "medium",
                Description: $"Stacks contain {threadPoolThreads} thread pool threads - analyze for starvation",
                Recommendation: "Review thread pool thread states and queue length for signs of thread pool exhaustion"
            ));
        }
    }

    private static void DetectStacksHighCPU(string[] lines, List<DetectedPattern> detectedPatterns)
    {
        var runningThreads = 0;

        foreach (var line in lines)
        {
            if (line.Contains("Running") || line.Contains("Preemptive") || line.Contains("CPU"))
            {
                runningThreads++;
            }
        }

        if (runningThreads > 5)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "high",
                Description: $"{runningThreads} threads in running state - potential CPU hotspot",
                Recommendation: "Identify hot methods appearing frequently across running thread stacks"
            ));
        }
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

        // .nettrace is a binary format - cannot use ReadAllText
        // This requires TraceEvent library or dotnet-trace convert for proper parsing
        summary.Findings.Add("Binary trace format requires TraceEvent library or dotnet-trace convert for detailed analysis.");
        summary.Recommendations.Add("Use dotnet-trace convert --format Speedscope to convert to a readable format for analysis.");
    }

    private static void AnalyzeCountersArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
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

    // NOTE: EventPipe heap analysis (.nettrace) removed due to vendored library issues
    // The EventPipeDotNetHeapDumper library has problems constructing MemoryGraph from EventPipe events
    // Use .gcdump files instead: mcp1_collect_gcdump + mcp1_analyze_heap

    [McpServerTool(Name = "analyze_heap"), Description("Analyze a .gcdump heap snapshot: type statistics (top N types), object list (with node IDs for find_retainer_paths), or diff comparison between two gcdumps. Returns JSON/text output only.")]
    public static async Task<HeapAnalysisResult> AnalyzeHeap(
        string artifactId,
        McpServer server,
        [Description("Analysis type: 'type_stats' (default) for top N types, 'objects' for object list with node IDs, 'diff' for comparison between two snapshots")] string analysisType = "type_stats",
        [Description("Metric: 'shallow_size' (default), 'count'")] string metric = "shallow_size",
        [Description("Analysis mode: 'auto' (default), 'reuse' (cache only), 'force' (re-analyze)")] string analysisMode = "auto",
        [Description("Maximum types to include in type_stats output, or maximum objects to include in objects output")] int maxTypes = 50,
        [Description("Baseline artifact ID for diff comparison")] string? baselineArtifactId = null,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();
        var preparer = server.Services!.GetRequiredService<IHeapSnapshotPreparer>();

        // Add 5-minute timeout to prevent hanging
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), linkedCts.Token);
            if (artifact == null)
            {
                return HeapAnalysisResult.Failure($"Artifact not found: {artifactId}");
            }

            if (artifact.Kind != ArtifactKind.GcDump)
            {
                return HeapAnalysisResult.Failure($"Artifact {artifactId} is not a GcDump.");
            }

            var metricKind = metric.ToLowerInvariant() switch
            {
                "count" => HeapMetricKind.Count,
                _ => HeapMetricKind.ShallowSize
            };

            var analysisModeKind = analysisMode.ToLowerInvariant() switch
            {
                "reuse" => HeapAnalysisMode.Reuse,
                "force" => HeapAnalysisMode.Force,
                _ => HeapAnalysisMode.Auto
            };

            var options = new HeapPreparationOptions
            {
                Metric = metricKind,
                AnalysisMode = analysisModeKind,
                GroupBy = HeapGroupBy.Type,
                MaxTypes = maxTypes
            };

            if (analysisType.ToLowerInvariant() == "diff")
            {
                if (string.IsNullOrEmpty(baselineArtifactId))
                {
                    return HeapAnalysisResult.Failure("baselineArtifactId is required for diff analysis");
                }

                var baselineArtifact = await artifactStore.GetAsync(new ArtifactId(baselineArtifactId), linkedCts.Token);
                if (baselineArtifact == null)
                {
                    return HeapAnalysisResult.Failure($"Baseline artifact not found: {baselineArtifactId}");
                }

                if (baselineArtifact.Kind != ArtifactKind.GcDump)
                {
                    return HeapAnalysisResult.Failure($"Baseline artifact {baselineArtifactId} is not a GcDump.");
                }

                var baselinePrepared = await preparer.PrepareAsync(baselineArtifact, options, linkedCts.Token);
                var targetPrepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

                var differLogger = server.Services!.GetRequiredService<ILogger<HeapSnapshotDiffer>>();
                var differ = new HeapSnapshotDiffer(differLogger);
                var diff = differ.Diff(baselinePrepared.Snapshot, targetPrepared.Snapshot);

                var diffData = new HeapDiffAnalysisData
                {
                    BaselineArtifactId = baselineArtifactId,
                    TargetArtifactId = artifactId,
                    Metric = metric.ToLowerInvariant(),
                    TotalHeapBytesBaseline = baselinePrepared.Snapshot.Metadata.TotalHeapBytes,
                    TotalHeapBytesTarget = targetPrepared.Snapshot.Metadata.TotalHeapBytes,
                    TotalObjectCountBaseline = baselinePrepared.Snapshot.Metadata.TotalObjectCount,
                    TotalObjectCountTarget = targetPrepared.Snapshot.Metadata.TotalObjectCount,
                    TypeDiffs = diff.TypeDiffs.Take(maxTypes).Select(td => new TypeDiffData
                    {
                        TypeName = td.TypeName,
                        Namespace = td.Namespace,
                        Status = td.Status.ToString(),
                        BaselineCount = td.BaselineCount,
                        TargetCount = td.TargetCount,
                        CountDelta = td.CountDelta,
                        BaselineShallowSize = td.BaselineShallowSize,
                        TargetShallowSize = td.TargetShallowSize,
                        ShallowSizeDelta = td.ShallowSizeDelta,
                        ShallowSizePercentChange = td.ShallowSizePercentChange
                    }).ToList()
                };

                return HeapAnalysisResult.Success(JsonSerializer.Serialize(diffData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }
            else if (analysisType.ToLowerInvariant() == "objects")
            {
                logger.LogInformation("Preparing heap snapshot for artifact {ArtifactId} (objects mode)", artifactId);
                var prepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

                // Sort nodes by metric (shallow size or count) and take top N
                var sortedNodes = metric.ToLowerInvariant() == "count"
                    ? prepared.Snapshot.Nodes.OrderByDescending(n => n.Count)
                    : prepared.Snapshot.Nodes.OrderByDescending(n => n.ShallowSizeBytes);

                var objectsData = new HeapObjectListData
                {
                    ArtifactId = artifactId,
                    Metric = metric.ToLowerInvariant(),
                    TotalHeapBytes = prepared.Snapshot.Metadata.TotalHeapBytes,
                    TotalObjectCount = prepared.Snapshot.Metadata.TotalObjectCount,
                    Objects = sortedNodes.Take(maxTypes).Select(node => new HeapObjectData
                    {
                        NodeId = node.NodeId.ToString(),
                        TypeName = node.TypeName,
                        Namespace = node.Namespace,
                        AssemblyName = node.AssemblyName,
                        ShallowSizeBytes = node.ShallowSizeBytes,
                        RetainedSizeBytes = node.RetainedSizeBytes,
                        Count = node.Count,
                        Generation = node.Generation
                    }).ToList()
                };

                return HeapAnalysisResult.Success(JsonSerializer.Serialize(objectsData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }
            else
            {
                logger.LogInformation("Preparing heap snapshot for artifact {ArtifactId}", artifactId);
                var prepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

                var typeStatsData = new HeapTypeStatsData
                {
                    ArtifactId = artifactId,
                    Metric = metric.ToLowerInvariant(),
                    TotalHeapBytes = prepared.Snapshot.Metadata.TotalHeapBytes,
                    TotalObjectCount = prepared.Snapshot.Metadata.TotalObjectCount,
                    TypeStats = prepared.Snapshot.TypeStats.Take(maxTypes).Select(ts => new TypeStatData
                    {
                        TypeName = ts.TypeName,
                        Namespace = ts.Namespace,
                        Count = ts.Count,
                        ShallowSizeBytes = ts.ShallowSizeBytes,
                        RetainedSizeBytes = ts.RetainedSizeBytes
                    }).ToList()
                };

                return HeapAnalysisResult.Success(JsonSerializer.Serialize(typeStatsData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogError("AnalyzeHeap timed out after 5 minutes for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure("AnalyzeHeap timed out after 5 minutes - the operation may be hanging");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("AnalyzeHeap was cancelled for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure("AnalyzeHeap was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Heap analysis failed for artifact {ArtifactId}. Stack: {StackTrace}", artifactId, ex.StackTrace);
            return HeapAnalysisResult.Failure($"Heap analysis failed: {ex.Message}\nStack: {ex.StackTrace}");
        }
    }

    [McpServerTool(Name = "find_retainer_paths"), Description("Find retainer paths from roots to a target object in heap snapshot. Returns JSON with paths showing object retention chains.")]
    public static async Task<HeapAnalysisResult> FindRetainerPaths(
        string artifactId,
        string targetNodeId,
        McpServer server,
        [Description("Maximum number of paths to return (default: 10)")] int maxPaths = 10,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();
        var preparer = server.Services!.GetRequiredService<IHeapSnapshotPreparer>();
        var dominatorCalculator = server.Services!.GetRequiredService<DominatorTreeCalculator>();

        // Add 5-minute timeout to prevent hanging
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), linkedCts.Token);
            if (artifact == null)
            {
                return HeapAnalysisResult.Failure($"Artifact not found: {artifactId}");
            }

            if (artifact.Kind != ArtifactKind.GcDump)
            {
                return HeapAnalysisResult.Failure($"Artifact {artifactId} is not a GcDump.");
            }

            // Parse target node ID
            if (!long.TryParse(targetNodeId, out var targetNodeIdLong))
            {
                return HeapAnalysisResult.Failure($"Invalid target node ID: {targetNodeId}");
            }

            // Prepare heap snapshot
            var options = new HeapPreparationOptions
            {
                Metric = HeapMetricKind.ShallowSize,
                AnalysisMode = HeapAnalysisMode.Auto,
                GroupBy = HeapGroupBy.Type,
                MaxTypes = 100
            };

            logger.LogInformation("Preparing heap snapshot for artifact {ArtifactId}", artifactId);
            var prepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

            // Build graph from snapshot
            var graph = new HeapGraphData
            {
                Nodes = prepared.Snapshot.Nodes.ToDictionary(n => n.NodeId),
                Edges = prepared.Snapshot.Edges.ToList(),
                Roots = prepared.Snapshot.Roots.ToList()
            };

            // Find retainer paths using DominatorTreeCalculator
            logger.LogInformation("Finding retainer paths for target node {TargetNodeId}", targetNodeId);
            var retainerPaths = dominatorCalculator.FindRetainerPaths(graph, targetNodeIdLong, maxPaths);

            // Convert to JSON
            var pathsData = retainerPaths.Select(rp => new RetainerPathData
            {
                RootNodeId = rp.RootNodeId,
                RootKind = rp.RootKind,
                TotalSteps = rp.TotalSteps,
                Steps = rp.Steps.Select(s => new RetainerPathStepData
                {
                    FromNodeId = s.FromNodeId,
                    ToNodeId = s.ToNodeId,
                    EdgeKind = s.EdgeKind,
                    IsWeak = s.IsWeak
                }).ToList()
            }).ToList();

            var result = new RetainerPathsResult
            {
                TargetNodeId = targetNodeId,
                PathCount = pathsData.Count,
                MaxPaths = maxPaths,
                Paths = pathsData
            };

            return HeapAnalysisResult.Success(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogError("FindRetainerPaths timed out after 5 minutes for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure("FindRetainerPaths timed out after 5 minutes - the operation may be hanging");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("FindRetainerPaths was cancelled for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure("FindRetainerPaths was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Find retainer paths failed for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure($"Find retainer paths failed: {ex.Message}");
        }
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

public record HeapAnalysisResult(
    bool IsSuccess,
    string Content,
    string? Error
)
{
    public static HeapAnalysisResult Success(string content) =>
        new(true, content, null);

    public static HeapAnalysisResult Failure(string error) =>
        new(false, string.Empty, error);
}

public record HeapTypeStatsData
{
    public string ArtifactId { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public long TotalHeapBytes { get; init; }
    public long TotalObjectCount { get; init; }
    public List<TypeStatData> TypeStats { get; init; } = new();
}

public record HeapObjectListData
{
    public string ArtifactId { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public long TotalHeapBytes { get; init; }
    public long TotalObjectCount { get; init; }
    public List<HeapObjectData> Objects { get; init; } = new();
}

public record HeapObjectData
{
    public string NodeId { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public long ShallowSizeBytes { get; init; }
    public long RetainedSizeBytes { get; init; }
    public int Count { get; init; }
    public string Generation { get; init; } = string.Empty;
}

public record HeapDiffAnalysisData
{
    public string BaselineArtifactId { get; init; } = string.Empty;
    public string TargetArtifactId { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public long TotalHeapBytesBaseline { get; init; }
    public long TotalHeapBytesTarget { get; init; }
    public long TotalObjectCountBaseline { get; init; }
    public long TotalObjectCountTarget { get; init; }
    public List<TypeDiffData> TypeDiffs { get; init; } = new();
}

public record TypeDiffData
{
    public string TypeName { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long BaselineCount { get; init; }
    public long TargetCount { get; init; }
    public long CountDelta { get; init; }
    public long BaselineShallowSize { get; init; }
    public long TargetShallowSize { get; init; }
    public long ShallowSizeDelta { get; init; }
    public double ShallowSizePercentChange { get; init; }
}

public record StackFrameData
{
    public int ThreadId { get; init; }
    public int FrameIndex { get; init; }
    public string CallSite { get; init; } = string.Empty;
}

public record StackFrame(string ChildSP, string IP, string CallSite);

public record ThreadStack(int ThreadId, string? ThreadName, StackFrame[] Frames);

public record StacksOutput(int Pid, DateTime Timestamp, ThreadStack[] Threads);

// Retainer paths result for MCP tool
public record RetainerPathsResult
{
    public string TargetNodeId { get; init; } = string.Empty;
    public int PathCount { get; init; }
    public int MaxPaths { get; init; }
    public required List<RetainerPathData> Paths { get; init; }
}

// Retainer path data for MCP tool
public record RetainerPathData
{
    public long RootNodeId { get; init; }
    public string RootKind { get; init; } = string.Empty;
    public int TotalSteps { get; init; }
    public required List<RetainerPathStepData> Steps { get; init; }
}

// Retainer path step data for MCP tool
public record RetainerPathStepData
{
    public long FromNodeId { get; init; }
    public long ToNodeId { get; init; }
    public string EdgeKind { get; init; } = string.Empty;
    public bool IsWeak { get; init; }
}

