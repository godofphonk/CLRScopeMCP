using ClrScope.Mcp.Domain.Artifacts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Analysis.PatternDetectors;

internal sealed class StacksPatternDetector : IPatternDetector
{
    private readonly ILogger<StacksPatternDetector> _logger;

    public ArtifactKind SupportedKind => ArtifactKind.Stacks;

    public StacksPatternDetector(ILogger<StacksPatternDetector> logger)
    {
        _logger = logger;
    }

    public async Task<DetectedPattern[]> DetectPatternsAsync(
        string filePath,
        string[] patternsToDetect,
        CancellationToken cancellationToken)
    {
        var detectedPatterns = new List<DetectedPattern>();

        try
        {
            if (!File.Exists(filePath))
            {
                return detectedPatterns.ToArray();
            }

            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pattern in patternsToDetect)
            {
                switch (pattern)
                {
                    case "deadlocks":
                        DetectDeadlocks(lines, detectedPatterns);
                        break;
                    case "thread_pool":
                        DetectThreadPool(lines, detectedPatterns);
                        break;
                    case "high_cpu":
                        DetectHighCPU(lines, detectedPatterns);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // If stacks file cannot be read, skip pattern detection
        }

        return detectedPatterns.ToArray();
    }

    private static void DetectDeadlocks(string[] lines, List<DetectedPattern> detectedPatterns)
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

    private static void DetectThreadPool(string[] lines, List<DetectedPattern> detectedPatterns)
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

    private static void DetectHighCPU(string[] lines, List<DetectedPattern> detectedPatterns)
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
}
