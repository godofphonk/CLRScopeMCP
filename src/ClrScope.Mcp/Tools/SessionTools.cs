using ClrScope.Mcp.Domain;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

[McpServerToolType]
public sealed class SessionTools
{
    [McpServerTool(Name = "session.get", Title = "Get Session", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Получение информации о сессии по ID")]
    public static async Task<SessionResult> GetSession(
        [Description("Session ID to get information for")] string sessionId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var sessionStore = server.Services.GetRequiredService<ISqliteSessionStore>();
        var artifactStore = server.Services.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services.GetRequiredService<ILogger<SessionTools>>();

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session ID must not be empty", nameof(sessionId));
        }

        try
        {
            var id = new SessionId(sessionId);
            var session = await sessionStore.GetAsync(id, cancellationToken);
            
            if (session == null)
            {
                logger.LogWarning("Session {SessionId} not found", sessionId);
                return new SessionResult(
                    Found: false,
                    SessionId: sessionId,
                    Kind: string.Empty,
                    Status: string.Empty,
                    Pid: 0,
                    StartedAtUtc: DateTime.UtcNow,
                    CompletedAtUtc: DateTime.UtcNow,
                    SessionError: string.Empty,
                    ArtifactCount: 0,
                    Artifacts: Array.Empty<SessionArtifactSummary>(),
                    Error: "Session not found"
                );
            }
            
            var artifacts = await artifactStore.GetBySessionAsync(id, cancellationToken);
            
            logger.LogInformation("Retrieved session {SessionId} with {ArtifactCount} artifacts", sessionId, artifacts.Count);
            
            return new SessionResult(
                Found: true,
                SessionId: session.SessionId.Value,
                Kind: session.Kind.ToString(),
                Status: session.Status.ToString(),
                Pid: session.Pid,
                StartedAtUtc: session.CreatedAtUtc,
                CompletedAtUtc: session.CompletedAtUtc,
                SessionError: session.Error,
                ArtifactCount: artifacts.Count,
                Artifacts: artifacts.Select(a => new SessionArtifactSummary(
                    a.ArtifactId.Value,
                    a.Kind.ToString(),
                    a.Status.ToString(),
                    a.FilePath,
                    a.SizeBytes
                )).ToArray(),
                Error: null
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for session retrieval: {Message}", ex.Message);
            return new SessionResult(
                Found: false,
                SessionId: sessionId,
                Kind: string.Empty,
                Status: string.Empty,
                Pid: 0,
                StartedAtUtc: DateTime.UtcNow,
                CompletedAtUtc: DateTime.UtcNow,
                SessionError: string.Empty,
                ArtifactCount: 0,
                Artifacts: Array.Empty<SessionArtifactSummary>(),
                Error: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Get session failed for {SessionId}", sessionId);
            return new SessionResult(
                Found: false,
                SessionId: sessionId,
                Kind: string.Empty,
                Status: string.Empty,
                Pid: 0,
                StartedAtUtc: DateTime.UtcNow,
                CompletedAtUtc: DateTime.UtcNow,
                SessionError: string.Empty,
                ArtifactCount: 0,
                Artifacts: Array.Empty<SessionArtifactSummary>(),
                Error: $"Get session failed: {ex.Message}"
            );
        }
    }
}

public record SessionResult(
    bool Found,
    string SessionId,
    string? Kind,
    string? Status,
    int Pid,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? SessionError,
    int ArtifactCount,
    SessionArtifactSummary[] Artifacts,
    string? Error
);

public record SessionArtifactSummary(
    string ArtifactId,
    string Kind,
    string Status,
    string FilePath,
    long SizeBytes
);
