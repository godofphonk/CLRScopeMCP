using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Analysis;

[McpServerToolType]
public sealed class SummaryTools
{
    [McpServerTool(Name = "artifact_summarize"), Description("Analyze and summarize an artifact with findings and recommendations")]
    public static async Task<ArtifactAnalysisResult> SummarizeArtifact(
        string artifactId,
        McpServer server,
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

            // Analyze the artifact based on its kind
            var analysis = AnalyzeArtifactContent(artifact, logger);
            return analysis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to summarize artifact: {ArtifactId}", artifactId);
            return ArtifactAnalysisResult.Failure($"Failed to summarize artifact: {ex.Message}");
        }
    }

    private static ArtifactAnalysisResult AnalyzeArtifactContent(Artifact artifact, ILogger logger)
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

            // Analyze based on kind
            switch (artifact.Kind)
            {
                case ArtifactKind.Stacks:
                    AnalyzeStacksArtifact(artifact, summary);
                    break;
                case ArtifactKind.Dump:
                    AnalyzeDumpArtifact(artifact, summary);
                    break;
                case ArtifactKind.GcDump:
                    AnalyzeGCDumpArtifact(artifact, summary);
                    break;
                case ArtifactKind.Trace:
                    AnalyzeTraceArtifact(artifact, summary);
                    break;
                case ArtifactKind.Counters:
                    AnalyzeCountersArtifact(artifact, summary);
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

    private static void AnalyzeStacksArtifact(Artifact artifact, ArtifactAnalysisSummary summary)
    {
        summary.Findings.Add("Stacks artifact contains managed thread stack traces.");
        summary.KeyMetrics["Thread Count"] = "Unknown (analyze file for details)";
        summary.Recommendations.Add("Use `analyze_dump_sos` with 'threads' and 'clrstack' commands to analyze.");
        summary.Recommendations.Add("Look for threads blocked on locks, monitors, or synchronization primitives.");
    }

    private static void AnalyzeDumpArtifact(Artifact artifact, ArtifactAnalysisSummary summary)
    {
        summary.Findings.Add("Memory dump contains full process memory snapshot.");
        summary.KeyMetrics["Dump Size"] = FormatBytes(artifact.SizeBytes);
        summary.Recommendations.Add("Use `analyze_dump_sos` to analyze the dump.");
        summary.Recommendations.Add("Check for large objects, memory leaks, and GC heap state.");
        summary.Recommendations.Add("Examine thread stacks for blocking patterns.");
    }

    private static void AnalyzeGCDumpArtifact(Artifact artifact, ArtifactAnalysisSummary summary)
    {
        summary.Findings.Add("GC dump contains heap snapshot with object graph.");
        summary.KeyMetrics["Heap Snapshot"] = "Available";
        summary.Recommendations.Add("Use dotnet-gcdump or dotnet-gcdump-analyzer to examine.");
        summary.Recommendations.Add("Check for large object arrays and strings.");
        summary.Recommendations.Add("Identify top types by size and count.");
    }

    private static void AnalyzeTraceArtifact(Artifact artifact, ArtifactAnalysisSummary summary)
    {
        summary.Findings.Add("Trace contains EventPipe events for performance analysis.");
        summary.KeyMetrics["Trace Size"] = FormatBytes(artifact.SizeBytes);
        summary.Recommendations.Add("Use PerfView or dotnet-trace analyze to examine.");
        summary.Recommendations.Add("Look for hot methods with high CPU time.");
        summary.Recommendations.Add("Check for allocation patterns and GC activity.");
    }

    private static void AnalyzeCountersArtifact(Artifact artifact, ArtifactAnalysisSummary summary)
    {
        summary.Findings.Add("Counters contain performance metrics over time.");
        summary.KeyMetrics["Metrics"] = "Performance counters";
        summary.Recommendations.Add("Examine CPU usage, memory, thread pool metrics.");
        summary.Recommendations.Add("Compare with baseline to identify anomalies.");
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
