using ClrScope.Mcp.Domain.Artifacts;

namespace ClrScope.Mcp.Services.Analysis.PatternDetectors;

internal interface IPatternDetector
{
    ArtifactKind SupportedKind { get; }
    Task<DetectedPattern[]> DetectPatternsAsync(
        string filePath,
        string[] patternsToDetect,
        CancellationToken cancellationToken);
}

public record DetectedPattern(
    string PatternType,
    string Severity,
    string Description,
    string Recommendation
);
