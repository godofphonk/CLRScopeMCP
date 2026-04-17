using ClrScope.Mcp.Domain.Artifacts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Analysis.PatternDetectors;

internal sealed class CountersPatternDetector : IPatternDetector
{
    private readonly ILogger<CountersPatternDetector> _logger;

    public ArtifactKind SupportedKind => ArtifactKind.Counters;

    public CountersPatternDetector(ILogger<CountersPatternDetector> logger)
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

            foreach (var pattern in patternsToDetect)
            {
                switch (pattern)
                {
                    case "high_cpu":
                        DetectHighCPU(fileContent, detectedPatterns);
                        break;
                    case "memory_leaks":
                        DetectMemoryLeaks(fileContent, detectedPatterns);
                        break;
                    case "thread_pool":
                        DetectThreadPool(fileContent, detectedPatterns);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // If counters file cannot be read, skip pattern detection
        }

        return detectedPatterns.ToArray();
    }

    private static void DetectHighCPU(string fileContent, List<DetectedPattern> detectedPatterns)
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

    private static void DetectMemoryLeaks(string fileContent, List<DetectedPattern> detectedPatterns)
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

    private static void DetectThreadPool(string fileContent, List<DetectedPattern> detectedPatterns)
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
}
