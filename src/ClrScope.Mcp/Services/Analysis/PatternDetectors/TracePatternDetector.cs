using ClrScope.Mcp.Domain.Artifacts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Analysis.PatternDetectors;

internal sealed class TracePatternDetector : IPatternDetector
{
    private readonly ILogger<TracePatternDetector> _logger;

    public ArtifactKind SupportedKind => ArtifactKind.Trace;

    public TracePatternDetector(ILogger<TracePatternDetector> logger)
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

            // .nettrace is a binary format - cannot use ReadAllTextAsync
            // This requires TraceEvent library or dotnet-trace convert for proper parsing
            // Pattern detection not available for binary trace files
            return detectedPatterns.ToArray();
        }
        catch (Exception)
        {
            // If trace file cannot be read, skip pattern detection
            return detectedPatterns.ToArray();
        }
    }
}
