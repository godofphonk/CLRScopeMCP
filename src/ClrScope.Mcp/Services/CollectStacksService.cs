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

public record CollectStacksRequest(int Pid);

public record CollectStacksResult(
    Session Session,
    Artifact? Artifact,
    string? Error)
{
    public static CollectStacksResult Success(Session session, Artifact artifact) =>
        new(session, artifact, null);

    public static CollectStacksResult Failure(Session session, string error) =>
        new(session, null, error);
}

public class CollectStacksService
{
    private readonly IOptions<ClrScopeOptions> _options;
    private readonly IPreflightValidator _preflightValidator;
    private readonly ISqliteSessionStore _sessionStore;
    private readonly ISqliteArtifactStore _artifactStore;
    private readonly IPidLockManager _pidLockManager;
    private readonly IActiveOperationRegistry _activeOperationRegistry;
    private readonly ICliCommandRunner _cliRunner;
    private readonly ICliToolAvailabilityChecker _availabilityChecker;
    private readonly ILogger<CollectStacksService> _logger;

    public CollectStacksService(
        IOptions<ClrScopeOptions> options,
        IPreflightValidator preflightValidator,
        ISqliteSessionStore sessionStore,
        ISqliteArtifactStore artifactStore,
        IPidLockManager pidLockManager,
        IActiveOperationRegistry activeOperationRegistry,
        ICliCommandRunner cliRunner,
        ICliToolAvailabilityChecker availabilityChecker,
        ILogger<CollectStacksService> logger)
    {
        _options = options;
        _preflightValidator = preflightValidator;
        _sessionStore = sessionStore;
        _artifactStore = artifactStore;
        _pidLockManager = pidLockManager;
        _activeOperationRegistry = activeOperationRegistry;
        _cliRunner = cliRunner;
        _availabilityChecker = availabilityChecker;
        _logger = logger;
    }

    public async Task<CollectStacksResult> CollectStacksAsync(
        CollectStacksRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0);

        // Check dotnet-stack availability
        var availability = await _availabilityChecker.CheckAvailabilityAsync("dotnet-stack", cancellationToken);
        if (!availability.IsAvailable)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Stacks, request.Pid, cancellationToken: cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = availability.InstallHint, Phase = SessionPhase.Failed }, cancellationToken);
            return CollectStacksResult.Failure(failedSession, availability.InstallHint ?? "dotnet-stack CLI not found");
        }

        // Acquire PID lock
        using var pidLock = await _pidLockManager.AcquireLockAsync(request.Pid, cancellationToken);

        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Stacks, request.Pid, cancellationToken: cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = preflightResult.Message, Phase = SessionPhase.Failed }, cancellationToken);
            return CollectStacksResult.Failure(failedSession, preflightResult.Message ?? "Preflight validation failed");
        }

        // Create session
        var session = await _sessionStore.CreateAsync(SessionKind.Stacks, request.Pid, cancellationToken: cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Running, Phase = SessionPhase.Attaching }, cancellationToken);

        // Create linked CTS
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeOperationRegistry.TryRegister(session.SessionId, operationCts);

        string? filePath = null;
        try
        {
            // Setup artifact path
            var artifactRoot = _options.Value.GetArtifactRoot();
            var stacksDir = Path.Combine(artifactRoot, "stacks");
            Directory.CreateDirectory(stacksDir);

            var fileName = $"stacks_{session.SessionId.Value}.txt";
            filePath = Path.Combine(stacksDir, fileName);

            progress?.Report(20);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Collecting }, operationCts.Token);

            // Collect stacks via dotnet-stack CLI
            _logger.LogInformation("Collecting managed stacks for PID {Pid} to {FilePath}", request.Pid, filePath);
            var result = await _cliRunner.ExecuteAsync("dotnet-stack", new[] { "report", "-p", request.Pid.ToString() }, operationCts.Token);

            if (result.ExitCode != 0)
            {
                var error = !string.IsNullOrEmpty(result.StandardError) ? result.StandardError : result.StandardOutput;
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = error, Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectStacksResult.Failure(session, error ?? "Stacks collection failed");
            }

            // Write output to file
            await File.WriteAllTextAsync(filePath, result.StandardOutput, operationCts.Token);

            // Check if file was created
            if (!File.Exists(filePath))
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Stacks file not created", Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectStacksResult.Failure(session, "Stacks file was not created");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Stacks file is empty", Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectStacksResult.Failure(session, "Stacks file is empty");
            }

            progress?.Report(70);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Persisting }, operationCts.Token);

            // Create artifact record
            var artifact = await _artifactStore.CreateAsync(
                ArtifactKind.Stacks,
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
            return CollectStacksResult.Success(updatedSession ?? session, updatedArtifact ?? artifact);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stacks collection cancelled for session {SessionId}", session.SessionId);
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Cancelled, CompletedAtUtc = DateTime.UtcNow, Phase = SessionPhase.Cancelled }, CancellationToken.None);
            return CollectStacksResult.Failure(session, "Stacks collection cancelled");
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
