using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Validation;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ClrScope.Mcp.Services;

public record CollectDumpRequest(int Pid, bool IncludeHeap = true);

public record CollectDumpResult(
    Session Session,
    Artifact? Artifact,
    string? Error
)
{
    public static CollectDumpResult Success(Session session, Artifact artifact) =>
        new(session, artifact, null);

    public static CollectDumpResult Failure(Session session, string error) =>
        new(session, null, error);
}

public class CollectDumpService
{
    private readonly IOptions<ClrScopeOptions> _options;
    private readonly ISqliteSessionStore _sessionStore;
    private readonly ISqliteArtifactStore _artifactStore;
    private readonly IPreflightValidator _preflightValidator;
    private readonly IPidLockManager _pidLockManager;
    private readonly IActiveOperationRegistry _activeOperationRegistry;
    private readonly ILogger<CollectDumpService> _logger;

    public CollectDumpService(
        IOptions<ClrScopeOptions> options,
        ISqliteSessionStore sessionStore,
        ISqliteArtifactStore artifactStore,
        IPreflightValidator preflightValidator,
        IPidLockManager pidLockManager,
        IActiveOperationRegistry activeOperationRegistry,
        ILogger<CollectDumpService> logger)
    {
        _options = options;
        _sessionStore = sessionStore;
        _artifactStore = artifactStore;
        _preflightValidator = preflightValidator;
        _pidLockManager = pidLockManager;
        _activeOperationRegistry = activeOperationRegistry;
        _logger = logger;
    }

    public async Task<CollectDumpResult> CollectDumpAsync(
        CollectDumpRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0);

        // Acquire PID lock to serialize operations on the same process
        using var pidLock = await _pidLockManager.AcquireLockAsync(request.Pid, cancellationToken);

        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Dump, request.Pid, cancellationToken: cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = preflightResult.Message, Phase = SessionPhase.Failed }, cancellationToken);
            return CollectDumpResult.Failure(failedSession, preflightResult.Message ?? "Preflight validation failed");
        }

        // Create session
        var session = await _sessionStore.CreateAsync(SessionKind.Dump, request.Pid, cancellationToken: cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Running, Phase = SessionPhase.Attaching }, cancellationToken);

        // Create linked CTS
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeOperationRegistry.TryRegister(session.SessionId, operationCts);

        string? filePath = null;
        try
        {
            // Setup artifact path
            var artifactRoot = _options.Value.GetArtifactRoot();
            var dumpsDir = Path.Combine(artifactRoot, "dumps");
            Directory.CreateDirectory(dumpsDir);

            var fileName = $"dump_{session.SessionId.Value}.dmp";
            filePath = Path.Combine(dumpsDir, fileName);

            // Write dump
            progress?.Report(20);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Collecting }, operationCts.Token);
            try
            {
                var client = new DiagnosticsClient(request.Pid);
                var dumpType = request.IncludeHeap ? DumpType.WithHeap : DumpType.Normal;

                // WriteDump is synchronous and doesn't support cancellation
                // Run it in a separate task to allow cancellation
                await Task.Run(() =>
                {
                    client.WriteDump(dumpType, filePath, false);
                }, operationCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Dump collection cancelled for session {SessionId}", session.SessionId);
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Cancelled, CompletedAtUtc = DateTime.UtcNow, Phase = SessionPhase.Cancelled }, CancellationToken.None);
                return CollectDumpResult.Failure(session, "Dump collection cancelled");
            }
            catch (Exception ex)
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = ex.Message, Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectDumpResult.Failure(session, $"Failed to write dump: {ex.Message}");
            }

        // Check if file was created and has valid size
        if (!File.Exists(filePath))
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Dump file not created", Phase = SessionPhase.Failed }, CancellationToken.None);
            return CollectDumpResult.Failure(session, "Dump file was not created");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Dump file is empty", Phase = SessionPhase.Failed }, CancellationToken.None);
            return CollectDumpResult.Failure(session, "Dump file is empty");
        }

        // Create artifact record
        progress?.Report(70);
        await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Persisting }, operationCts.Token);
        var fileSize = new FileInfo(filePath).Length;

        var artifact = await _artifactStore.CreateAsync(
            ArtifactKind.Dump,
            filePath,
            fileSize,
            request.Pid,
            session.SessionId,
            operationCts.Token
        );

        // Use the ArtifactId from store for URIs (fix double generation)
        var diagUri = $"clrscope://artifact/{artifact.ArtifactId.Value}";
        var fileUri = $"file://{filePath}";

        // Update artifact with URIs
        artifact = artifact with { DiagUri = diagUri, FileUri = fileUri };
        await _artifactStore.UpdateAsync(artifact with { Status = ArtifactStatus.Completed }, CancellationToken.None);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Completed, CompletedAtUtc = DateTime.UtcNow, Phase = SessionPhase.Completed }, CancellationToken.None);

        // Re-read to get updated state
        var updatedSession = await _sessionStore.GetAsync(session.SessionId, CancellationToken.None);
        var updatedArtifact = await _artifactStore.GetAsync(artifact.ArtifactId, CancellationToken.None);

        progress?.Report(100);
        return CollectDumpResult.Success(updatedSession ?? session, updatedArtifact ?? artifact);
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
}
