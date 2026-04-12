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

        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID must not be empty", nameof(sessionId));
            }

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
                SessionError: session.Error ?? string.Empty,
                ArtifactCount: artifacts.Count,
                Artifacts: artifacts.Select(a => new SessionArtifactSummary(
                    a.ArtifactId.Value,
                    a.Kind.ToString(),
                    a.Status.ToString(),
                    a.FilePath,
                    a.SizeBytes
                )).ToArray(),
                Error: string.Empty
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

    [McpServerTool(Name = "session.cancel", Title = "Cancel Session", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true), Description("Отмена активной сессии")]
    public static async Task<CancelSessionResult> CancelSession(
        [Description("Session ID to cancel")] string sessionId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var sessionStore = server.Services.GetRequiredService<ISqliteSessionStore>();
        var logger = server.Services.GetRequiredService<ILogger<SessionTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID must not be empty", nameof(sessionId));
            }

            var id = new SessionId(sessionId);
            var session = await sessionStore.GetAsync(id, cancellationToken);

            if (session == null)
            {
                logger.LogWarning("Session {SessionId} not found for cancellation", sessionId);
                return new CancelSessionResult(
                    Success: false,
                    SessionId: sessionId,
                    Message: "Session not found"
                );
            }

            if (session.Status == SessionStatus.Completed || session.Status == SessionStatus.Cancelled || session.Status == SessionStatus.Failed)
            {
                logger.LogWarning("Session {SessionId} is already in terminal state {Status}", sessionId, session.Status);
                return new CancelSessionResult(
                    Success: false,
                    SessionId: sessionId,
                    Message: $"Session is already in terminal state: {session.Status}"
                );
            }

            await sessionStore.UpdateAsync(session with { Status = SessionStatus.Cancelled, CompletedAtUtc = DateTime.UtcNow }, cancellationToken);
            logger.LogInformation("Cancelled session {SessionId}", sessionId);

            return new CancelSessionResult(
                Success: true,
                SessionId: sessionId,
                Message: "Session cancelled successfully"
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for session cancellation: {Message}", ex.Message);
            return new CancelSessionResult(
                Success: false,
                SessionId: sessionId,
                Message: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cancel session failed for {SessionId}", sessionId);
            return new CancelSessionResult(
                Success: false,
                SessionId: sessionId,
                Message: $"Cancel session failed: {ex.Message}"
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

public record CancelSessionResult(
    bool Success,
    string SessionId,
    string Message
);

public record SessionArtifactSummary(
    string ArtifactId,
    string Kind,
    string Status,
    string FilePath,
    long SizeBytes
);
