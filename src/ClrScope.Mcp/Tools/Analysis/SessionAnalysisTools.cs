using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Analysis;

[McpServerToolType]
public sealed class SessionAnalysisTools
{
    [McpServerTool(Name = "session_analyze"), Description("Analyze a diagnostic session with all its artifacts")]
    public static async Task<SessionAnalysisResult> AnalyzeSession(
        string sessionId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var sessionStore = server.Services!.GetRequiredService<ISqliteSessionStore>();
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SessionAnalysisTools>>();

        try
        {
            var session = await sessionStore.GetAsync(new SessionId(sessionId), cancellationToken);
            if (session == null)
            {
                return SessionAnalysisResult.Failure($"Session not found: {sessionId}");
            }

            var artifacts = await artifactStore.GetBySessionAsync(new SessionId(sessionId), cancellationToken);
            var analysis = AnalyzeSession(session, artifacts, logger);
            return analysis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze session: {SessionId}", sessionId);
            return SessionAnalysisResult.Failure($"Failed to analyze session: {ex.Message}");
        }
    }

    private static SessionAnalysisResult AnalyzeSession(Session session, IReadOnlyList<Artifact> artifacts, ILogger logger)
    {
        try
        {
            var summary = new SessionSummary(
                SessionId: session.SessionId.Value,
                Kind: session.Kind.ToString(),
                Status: session.Status.ToString(),
                Pid: session.Pid,
                StartedAt: session.CreatedAtUtc,
                CompletedAt: session.CompletedAtUtc,
                Phase: session.Phase.ToString(),
                ArtifactCount: artifacts.Count,
                Artifacts: artifacts.Select(a => new ArtifactInfo(
                    a.ArtifactId.Value,
                    a.Kind.ToString(),
                    a.Status.ToString(),
                    a.SizeBytes,
                    a.FilePath
                )).ToList(),
                KeyMetrics: new Dictionary<string, string>(),
                Issues: new List<string>(),
                Recommendations: new List<string>()
            );

            // Add basic metrics
            summary.KeyMetrics["Total Size"] = FormatBytes(artifacts.Sum(a => a.SizeBytes));
            summary.KeyMetrics["Artifact Count"] = artifacts.Count.ToString();
            summary.KeyMetrics["Status"] = session.Status.ToString();
            summary.KeyMetrics["Phase"] = session.Phase.ToString();

            if (session.CompletedAtUtc.HasValue)
            {
                var duration = session.CompletedAtUtc.Value - session.CreatedAtUtc;
                summary.KeyMetrics["Duration"] = duration.TotalSeconds.ToString("F2") + "s";
            }

            // Analyze artifacts
            foreach (var artifact in artifacts)
            {
                if (artifact.Status == ArtifactStatus.Failed)
                {
                    summary.Issues.Add($"Artifact {artifact.ArtifactId.Value} ({artifact.Kind}) failed to collect.");
                    summary.Recommendations.Add($"Check logs for {artifact.ArtifactId.Value} to determine failure cause.");
                }
            }

            // Analyze session status
            if (session.Status == SessionStatus.Failed)
            {
                summary.Issues.Add("Session collection failed.");
                summary.Recommendations.Add("Review error logs to identify the root cause.");
                summary.Recommendations.Add("Retry collection if the issue is transient.");
            }
            else if (session.Status == SessionStatus.Completed)
            {
                summary.Recommendations.Add("Session collection completed successfully.");
                summary.Recommendations.Add("Use artifact-specific analysis tools to examine collected data.");
            }

            // Analyze based on session kind
            switch (session.Kind)
            {
                case SessionKind.Stacks:
                    summary.Recommendations.Add("Use `analyze_dump_sos` with stack-related commands to analyze.");
                    break;
                case SessionKind.Dump:
                    summary.Recommendations.Add("Use `analyze_dump_sos` to examine the memory dump.");
                    break;
                case SessionKind.GcDump:
                    summary.Recommendations.Add("Use dotnet-gcdump tools to analyze GC heap state.");
                    break;
                case SessionKind.Trace:
                    summary.Recommendations.Add("Use PerfView or dotnet-trace analyze to examine the trace.");
                    break;
                case SessionKind.Counters:
                    summary.Recommendations.Add("Examine performance metrics over time.");
                    break;
                default:
                    summary.Recommendations.Add($"Session kind {session.Kind} analysis not fully implemented.");
                    break;
            }

            return SessionAnalysisResult.Success(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze session content");
            return SessionAnalysisResult.Failure($"Failed to analyze session content: {ex.Message}");
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
}

public record SessionAnalysisResult(
    bool IsSuccess,
    SessionSummary? Summary,
    string? Error
)
{
    public static SessionAnalysisResult Success(SessionSummary summary) =>
        new(true, summary, null);

    public static SessionAnalysisResult Failure(string error) =>
        new(false, null, error);
}

public record SessionSummary(
    string SessionId,
    string Kind,
    string Status,
    int Pid,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Phase,
    int ArtifactCount,
    List<ArtifactInfo> Artifacts,
    Dictionary<string, string> KeyMetrics,
    List<string> Issues,
    List<string> Recommendations
);

public record ArtifactInfo(
    string ArtifactId,
    string Kind,
    string Status,
    long SizeBytes,
    string FilePath
);
