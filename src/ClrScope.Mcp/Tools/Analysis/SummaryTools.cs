using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

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
        var analyzer = server.Services!.GetRequiredService<ArtifactContentAnalyzer>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return ArtifactAnalysisResult.Failure($"Artifact not found: {artifactId}");
            }

            // Analyze the artifact based on its kind and focus
            var analysis = analyzer.Analyze(artifact, focus.ToLowerInvariant());
            return analysis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to summarize artifact: {ArtifactId}", artifactId);
            return ArtifactAnalysisResult.Failure($"Failed to summarize artifact: {ex.Message}");
        }
    }
}
