using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Analysis.PatternDetectors;

internal sealed class GcDumpPatternDetector : IPatternDetector
{
    private readonly ISosAnalyzer _sosAnalyzer;
    private readonly ILogger<GcDumpPatternDetector> _logger;

    public ArtifactKind SupportedKind => ArtifactKind.GcDump;

    public GcDumpPatternDetector(ISosAnalyzer sosAnalyzer, ILogger<GcDumpPatternDetector> logger)
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
                case "excessive_allocations":
                    await DetectExcessiveAllocations(filePath, detectedPatterns, cancellationToken);
                    break;
            }
        }

        return detectedPatterns.ToArray();
    }

    private async Task DetectMemoryLeaks(string filePath, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        detectedPatterns.Add(new DetectedPattern(
            PatternType: "memory_leak",
            Severity: "medium",
            Description: "GC dump analysis for memory leaks requires dotnet-gcdump-analyzer",
            Recommendation: "Use dotnet-gcdump-analyze to examine heap growth and object references"
        ));
    }

    private async Task DetectExcessiveAllocations(string filePath, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        detectedPatterns.Add(new DetectedPattern(
            PatternType: "excessive_allocations",
            Severity: "medium",
            Description: "GC dump allocation analysis requires dotnet-gcdump-analyzer",
            Recommendation: "Use dotnet-gcdump-analyze to examine allocation rates and patterns"
        ));
    }
}
