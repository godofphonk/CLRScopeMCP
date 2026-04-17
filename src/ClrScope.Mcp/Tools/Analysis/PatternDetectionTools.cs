using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Analysis.PatternDetectors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Analysis;

[McpServerToolType]
public sealed class PatternDetectionTools
{
    [McpServerTool(Name = "detect_patterns"), Description("Automatically detect common problem patterns (memory leaks, deadlocks, thread pool starvation, high CPU)")]
    public static async Task<PatternDetectionResult> DetectPatterns(
        string artifactId,
        McpServer server,
        [Description("Pattern types to detect: 'all' (default), 'memory_leaks', 'deadlocks', 'thread_pool', 'high_cpu', 'excessive_allocations'")] string patternTypes = "all",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var sosAnalyzer = server.Services!.GetRequiredService<ISosAnalyzer>();
        var logger = server.Services!.GetRequiredService<ILogger<PatternDetectionTools>>();
        var options = server.Services!.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClrScopeOptions>>();
        var detectors = server.Services!.GetServices<IPatternDetector>();

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

            // Check if dotnet-dump is available for Dump and GcDump artifacts
            if (artifact.Kind == ArtifactKind.Dump || artifact.Kind == ArtifactKind.GcDump)
            {
                if (!await sosAnalyzer.IsAvailableAsync(cancellationToken))
                {
                    return PatternDetectionResult.Failure("dotnet-dump CLI not found. Pattern detection for Dump/GcDump requires SOS commands.");
                }
            }

            var patternsToDetect = patternTypes.ToLowerInvariant() == "all"
                ? new[] { "memory_leaks", "deadlocks", "thread_pool", "high_cpu", "excessive_allocations" }
                : patternTypes.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Find appropriate detector for this artifact kind
            var detector = detectors.FirstOrDefault(d => d.SupportedKind == artifact.Kind);
            if (detector == null)
            {
                return PatternDetectionResult.Failure($"Pattern detection not supported for artifact kind: {artifact.Kind}");
            }

            var detectedPatterns = await detector.DetectPatternsAsync(artifact.FilePath, patternsToDetect, cancellationToken);
            return PatternDetectionResult.Success(detectedPatterns);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pattern detection failed for artifact {ArtifactId}", artifactId);
            return PatternDetectionResult.Failure($"Pattern detection failed: {ex.Message}");
        }
    }
}

public record PatternDetectionResult(
    bool IsSuccess,
    DetectedPattern[] Patterns,
    string? Error)
{
    public static PatternDetectionResult Success(DetectedPattern[] patterns) =>
        new(true, patterns, null);

    public static PatternDetectionResult Failure(string error) =>
        new(false, Array.Empty<DetectedPattern>(), error);
}
