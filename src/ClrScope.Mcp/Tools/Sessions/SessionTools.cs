using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Sessions;

[McpServerToolType]
public sealed class SessionTools
{
    [McpServerTool(Name = "session_get", Title = "Get Session Info", ReadOnly = true, Idempotent = true, UseStructuredContent = true), Description("Get information about a diagnostic session")]
    public static async Task<SessionResult> GetSession(
        [Description("Session ID to get information for")] string sessionId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var sessionStore = server.Services!.GetRequiredService<ISqliteSessionStore>();
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SessionTools>>();

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
                    Kind: null,
                    Status: null,
                    Pid: 0,
                    StartedAtUtc: null,
                    CompletedAtUtc: null,
                    SessionError: null,
                    ArtifactCount: 0,
                    Artifacts: Array.Empty<SessionArtifactSummary>(),
                    Error: "Session not found"
                );
            }

            var artifacts = await artifactStore.GetBySessionAsync(id, cancellationToken);

            logger.LogInformation("Retrieved session {SessionId} with {ArtifactCount} artifacts, phase {Phase}", sessionId, artifacts.Count, session.Phase);

            return new SessionResult(
                Found: true,
                SessionId: sessionId,
                Kind: session.Kind.ToString(),
                Status: session.Status.ToString(),
                Pid: session.Pid,
                StartedAtUtc: session.CreatedAtUtc,
                CompletedAtUtc: session.CompletedAtUtc,
                SessionError: session.Error,
                ArtifactCount: artifacts.Count,
                Artifacts: artifacts.Select(a => new SessionArtifactSummary(
                    ArtifactId: a.ArtifactId.Value,
                    Kind: a.Kind.ToString(),
                    Status: a.Status.ToString(),
                    FilePath: a.FilePath,
                    SizeBytes: a.SizeBytes
                )).ToArray(),
                Error: null,
                Phase: session.Phase.ToString()
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for session get: {Message}", ex.Message);
            return new SessionResult(
                Found: false,
                SessionId: sessionId,
                Kind: null,
                Status: null,
                Pid: 0,
                StartedAtUtc: null,
                CompletedAtUtc: null,
                SessionError: null,
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
                Kind: null,
                Status: null,
                Pid: 0,
                StartedAtUtc: null,
                CompletedAtUtc: null,
                SessionError: null,
                ArtifactCount: 0,
                Artifacts: Array.Empty<SessionArtifactSummary>(),
                Error: $"Get session failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "session_cancel", Title = "Cancel Session", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true), Description("Cancel an active session")]
    public static async Task<CancelSessionResult> CancelSession(
        [Description("Session ID to cancel")] string sessionId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var sessionStore = server.Services!.GetRequiredService<ISqliteSessionStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SessionTools>>();

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

            // Cancel the active operation via registry
            var activeOperationRegistry = server.Services!.GetRequiredService<IActiveOperationRegistry>();
            var cancelled = activeOperationRegistry.TryCancel(session.SessionId, "Session cancelled via session.cancel");

            if (!cancelled)
            {
                logger.LogWarning("Failed to cancel session {SessionId} - operation not found or already completed", sessionId);
                return new CancelSessionResult(
                    Success: false,
                    SessionId: sessionId,
                    Message: "Session operation not found or already completed"
                );
            }

            // Update session status in database
            await sessionStore.UpdateAsync(session with { Status = SessionStatus.Cancelled, CompletedAtUtc = DateTime.UtcNow, Phase = SessionPhase.Cancelled }, cancellationToken);
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
    string? Error,
    string? Phase = null
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
