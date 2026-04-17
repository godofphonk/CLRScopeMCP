using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Analysis.PatternDetectors;

internal sealed class DumpPatternDetector : IPatternDetector
{
    private readonly ISosAnalyzer _sosAnalyzer;
    private readonly ILogger<DumpPatternDetector> _logger;

    public ArtifactKind SupportedKind => ArtifactKind.Dump;

    public DumpPatternDetector(ISosAnalyzer sosAnalyzer, ILogger<DumpPatternDetector> logger)
    {
        _sosAnalyzer = sosAnalyzer;
        _logger = logger;
    }

    public async Task<DetectedPattern[]> DetectPatternsAsync(
        string filePath,
        string[] patternsToDetect,
        CancellationToken cancellationToken)
    {
        var detectedPatterns = new List<DetectedPattern>();

        foreach (var pattern in patternsToDetect)
        {
            switch (pattern)
            {
                case "memory_leaks":
                    await DetectMemoryLeaks(filePath, detectedPatterns, cancellationToken);
                    break;
                case "deadlocks":
                    await DetectDeadlocks(filePath, detectedPatterns, cancellationToken);
                    break;
                case "thread_pool":
                    await DetectThreadPoolIssues(filePath, detectedPatterns, cancellationToken);
                    break;
                case "high_cpu":
                    await DetectHighCPU(filePath, detectedPatterns, cancellationToken);
                    break;
                case "excessive_allocations":
                    await DetectExcessiveAllocations(filePath, detectedPatterns, cancellationToken);
                    break;
            }
        }

        return detectedPatterns.ToArray();
    }

    private async Task DetectMemoryLeaks(string filePath, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await _sosAnalyzer.ExecuteCommandAsync(filePath, "dumpheap -stat", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
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

    private async Task DetectDeadlocks(string filePath, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await _sosAnalyzer.ExecuteCommandAsync(filePath, "threads", cancellationToken);
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

    private async Task DetectThreadPoolIssues(string filePath, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await _sosAnalyzer.ExecuteCommandAsync(filePath, "threadpool", cancellationToken);
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

    private async Task DetectHighCPU(string filePath, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await _sosAnalyzer.ExecuteCommandAsync(filePath, "threads", cancellationToken);
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

    private async Task DetectExcessiveAllocations(string filePath, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await _sosAnalyzer.ExecuteCommandAsync(filePath, "dumpheap -stat", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
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
