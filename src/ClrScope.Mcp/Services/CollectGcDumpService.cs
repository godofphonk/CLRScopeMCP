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

public record CollectGcDumpRequest(int Pid);

public record CollectGcDumpResult(
    Session Session,
    Artifact? Artifact,
    string? Error)
{
    public static CollectGcDumpResult Success(Session session, Artifact artifact) =>
        new(session, artifact, null);

    public static CollectGcDumpResult Failure(Session session, string error) =>
        new(session, null, error);
}

public class CollectGcDumpService
{
    private readonly IOptions<ClrScopeOptions> _options;
    private readonly IPreflightValidator _preflightValidator;
    private readonly ISqliteSessionStore _sessionStore;
    private readonly ISqliteArtifactStore _artifactStore;
    private readonly IPidLockManager _pidLockManager;
    private readonly IActiveOperationRegistry _activeOperationRegistry;
    private readonly ICliCommandRunner _cliRunner;
    private readonly ICliToolAvailabilityChecker _availabilityChecker;
    private readonly ILogger<CollectGcDumpService> _logger;

    public CollectGcDumpService(
        IOptions<ClrScopeOptions> options,
        IPreflightValidator preflightValidator,
        ISqliteSessionStore sessionStore,
        ISqliteArtifactStore artifactStore,
        IPidLockManager pidLockManager,
        IActiveOperationRegistry activeOperationRegistry,
        ICliCommandRunner cliRunner,
        ICliToolAvailabilityChecker availabilityChecker,
        ILogger<CollectGcDumpService> logger)
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

    public async Task<CollectGcDumpResult> CollectGcDumpAsync(
        CollectGcDumpRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0);

        // Check dotnet-gcdump availability
        var availability = await _availabilityChecker.CheckAvailabilityAsync("dotnet-gcdump", cancellationToken);
        if (!availability.IsAvailable)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.GcDump, request.Pid, cancellationToken: cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = availability.InstallHint, Phase = SessionPhase.Failed }, cancellationToken);
            return CollectGcDumpResult.Failure(failedSession, availability.InstallHint ?? "dotnet-gcdump CLI not found");
        }

        // Acquire PID lock
        using var pidLock = await _pidLockManager.AcquireLockAsync(request.Pid, cancellationToken);

        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.GcDump, request.Pid, cancellationToken: cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = preflightResult.Message, Phase = SessionPhase.Failed }, cancellationToken);
            return CollectGcDumpResult.Failure(failedSession, preflightResult.Message ?? "Preflight validation failed");
        }

        // Create session
        var session = await _sessionStore.CreateAsync(SessionKind.GcDump, request.Pid, cancellationToken: cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Running, Phase = SessionPhase.Attaching }, cancellationToken);

        // Create linked CTS
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeOperationRegistry.TryRegister(session.SessionId, operationCts);

        try
        {
            // Setup artifact path
            var artifactRoot = _options.Value.GetArtifactRoot();
            var gcdumpsDir = Path.Combine(artifactRoot, "gcdumps");
            Directory.CreateDirectory(gcdumpsDir);

            var fileName = $"gcdump_{session.SessionId.Value}.gcdump";
            var filePath = Path.Combine(gcdumpsDir, fileName);

            progress?.Report(20);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Collecting }, operationCts.Token);

            // Collect GC dump via dotnet-gcdump CLI
            _logger.LogInformation("Collecting GC dump for PID {Pid} to {FilePath}", request.Pid, filePath);
            var result = await _cliRunner.ExecuteAsync("dotnet-gcdump", new[] { "collect", "-p", request.Pid.ToString(), "-o", filePath }, operationCts.Token);

            if (result.ExitCode != 0)
            {
                var error = !string.IsNullOrEmpty(result.StandardError) ? result.StandardError : result.StandardOutput;
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = error, Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectGcDumpResult.Failure(session, error ?? "GC dump collection failed");
            }

            // Check if file was created
            if (!File.Exists(filePath))
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "GC dump file not created", Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectGcDumpResult.Failure(session, "GC dump file was not created");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "GC dump file is empty", Phase = SessionPhase.Failed }, CancellationToken.None);
                return CollectGcDumpResult.Failure(session, "GC dump file is empty");
            }

            progress?.Report(70);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Persisting }, operationCts.Token);

            // Create artifact record
            var artifact = await _artifactStore.CreateAsync(
                ArtifactKind.GcDump,
                filePath,
                fileInfo.Length,
                request.Pid,
                session.SessionId,
                operationCts.Token
            );

            // Update artifact with URIs
            var diagUri = $"clrscope://artifact/{artifact.ArtifactId.Value}";
            var fileUri = $"file://{filePath}";
            artifact = artifact with { DiagUri = diagUri, FileUri = fileUri };
            await _artifactStore.UpdateAsync(artifact with { Status = ArtifactStatus.Completed }, CancellationToken.None);

            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Completed, CompletedAtUtc = DateTime.UtcNow, Phase = SessionPhase.Completed }, CancellationToken.None);

            progress?.Report(100);
            return CollectGcDumpResult.Success(session, artifact);
        }
        finally
        {
            _activeOperationRegistry.Complete(session.SessionId);
        }
    }
}
