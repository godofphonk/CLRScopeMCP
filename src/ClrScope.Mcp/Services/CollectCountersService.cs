using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ClrScope.Mcp.Services;

public record CollectCountersRequest(int Pid, string Duration, string[] Providers);

public record CollectCountersResult(
    Session Session,
    Artifact? Artifact,
    string? Error
)
{
    public static CollectCountersResult Success(Session session, Artifact artifact) =>
        new(session, artifact, null);

    public static CollectCountersResult Failure(Session session, string error) =>
        new(session, null, error);
}

public class CollectCountersService
{
    private readonly IOptions<ClrScopeOptions> _options;
    private readonly IPreflightValidator _preflightValidator;
    private readonly ISqliteSessionStore _sessionStore;
    private readonly ISqliteArtifactStore _artifactStore;
    private readonly IPidLockManager _pidLockManager;
    private readonly IActiveOperationRegistry _activeOperationRegistry;
    private readonly ICountersBackend _countersBackend;
    private readonly ILogger<CollectCountersService> _logger;

    public CollectCountersService(
        IOptions<ClrScopeOptions> options,
        IPreflightValidator preflightValidator,
        ISqliteSessionStore sessionStore,
        ISqliteArtifactStore artifactStore,
        IPidLockManager pidLockManager,
        IActiveOperationRegistry activeOperationRegistry,
        ICountersBackend countersBackend,
        ILogger<CollectCountersService> logger)
    {
        _options = options;
        _preflightValidator = preflightValidator;
        _sessionStore = sessionStore;
        _artifactStore = artifactStore;
        _pidLockManager = pidLockManager;
        _activeOperationRegistry = activeOperationRegistry;
        _countersBackend = countersBackend;
        _logger = logger;
    }

    public async Task<CollectCountersResult> CollectCountersAsync(
        CollectCountersRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0);

        // Acquire PID lock
        using var pidLock = await _pidLockManager.AcquireLockAsync(request.Pid, cancellationToken);

        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Counters, request.Pid, cancellationToken: cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = preflightResult.Message, Phase = SessionPhase.Failed }, cancellationToken);
            return CollectCountersResult.Failure(failedSession, preflightResult.Message ?? "Preflight validation failed");
        }

        // Parse duration
        TimeSpan duration;
        try
        {
            duration = ParseDuration(request.Duration);
        }
        catch (FormatException ex)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Counters, request.Pid, cancellationToken: cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = ex.Message, Phase = SessionPhase.Failed }, cancellationToken);
            return CollectCountersResult.Failure(failedSession, $"Invalid duration format: {ex.Message}");
        }

        // Create session
        var session = await _sessionStore.CreateAsync(SessionKind.Counters, request.Pid, cancellationToken: cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Running, Phase = SessionPhase.Attaching }, cancellationToken);

        // Create linked CTS
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeOperationRegistry.TryRegister(session.SessionId, operationCts);

        string? filePath = null;
        try
        {
            // Setup artifact path
            var artifactRoot = _options.Value.GetArtifactRoot();
            var countersDir = Path.Combine(artifactRoot, "counters");
            Directory.CreateDirectory(countersDir);

            var fileName = $"counters_{session.SessionId.Value}.csv";
            filePath = Path.Combine(countersDir, fileName);

            progress?.Report(20);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Collecting }, operationCts.Token);

            // Collect counters via backend
            var countersResult = await _countersBackend.CollectAsync(
                request.Pid,
                request.Providers,
                duration,
                filePath,
                operationCts.Token);

            if (!countersResult.Success)
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = countersResult.Error, Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectCountersResult.Failure(session, countersResult.Error ?? "Counter collection failed");
            }

            // Check if file was created
            if (!File.Exists(filePath))
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Counter file not created", Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectCountersResult.Failure(session, "Counter file was not created");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Counter file is empty", Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectCountersResult.Failure(session, "Counter file is empty");
            }

            progress?.Report(70);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Persisting }, operationCts.Token);

            // Create artifact record
            var artifact = await _artifactStore.CreateAsync(
                ArtifactKind.Counters,
                filePath,
                fileInfo.Length,
                request.Pid,
                session.SessionId,
                operationCts.Token
            );

            // Update artifact with URIs
            var diagUri = $"clrscope://artifact/{artifact.ArtifactId.Value}";
            var fileUri = new Uri(filePath).AbsoluteUri;
            artifact = artifact with { DiagUri = diagUri, FileUri = fileUri };
            await _artifactStore.UpdateAsync(artifact with { Status = ArtifactStatus.Completed }, CancellationToken.None);

            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Completed, CompletedAtUtc = DateTime.UtcNow, Phase = SessionPhase.Completed }, CancellationToken.None);

            // Re-read to get updated state
            var updatedSession = await _sessionStore.GetAsync(session.SessionId, CancellationToken.None);
            var updatedArtifact = await _artifactStore.GetAsync(artifact.ArtifactId, CancellationToken.None);

            progress?.Report(100);
            return CollectCountersResult.Success(updatedSession ?? session, updatedArtifact ?? artifact);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Counters collection cancelled for session {SessionId}", session.SessionId);
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Cancelled, CompletedAtUtc = DateTime.UtcNow, Phase = SessionPhase.Cancelled }, CancellationToken.None);
            return CollectCountersResult.Failure(session, "Counters collection cancelled");
        }
        catch (Exception ex) when (!operationCts.Token.IsCancellationRequested)
        {
            // Best-effort: mark session as failed
            try
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, CompletedAtUtc = DateTime.UtcNow, Phase = SessionPhase.Failed, Error = ex.Message }, CancellationToken.None);
                _logger.LogInformation("Marked session {SessionId} as failed due to exception: {Error}", session.SessionId, ex.Message);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to mark session {SessionId} as failed", session.SessionId);
            }

            // Cleanup orphaned file on unexpected failure
            if (filePath != null && File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Cleaned up orphaned file: {FilePath}", filePath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to cleanup orphaned file: {FilePath}", filePath);
                }
            }
            throw;
        }
        finally
        {
            _activeOperationRegistry.Complete(session.SessionId);
        }
    }

    private static TimeSpan ParseDuration(string duration)
    {
        // Parse hh:mm:ss format
        var parts = duration.Split(':');
        if (parts.Length != 3)
        {
            throw new FormatException("Duration must be in hh:mm:ss format");
        }

        var hours = int.Parse(parts[0]);
        var minutes = int.Parse(parts[1]);
        var seconds = int.Parse(parts[2]);

        return new TimeSpan(hours, minutes, seconds);
    }
}
