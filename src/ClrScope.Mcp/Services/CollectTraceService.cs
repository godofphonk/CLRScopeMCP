using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Domain;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Validation;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.Diagnostics.Tracing;

namespace ClrScope.Mcp.Services;

public record CollectTraceRequest(int Pid, string Duration, string? Profile = null);

public record CollectTraceResult(
    Session Session,
    Artifact? Artifact,
    string? Error,
    TraceCompletionMode CompletionMode = TraceCompletionMode.Complete
)
{
    public static CollectTraceResult Success(Session session, Artifact artifact, TraceCompletionMode completionMode = TraceCompletionMode.Complete) =>
        new(session, artifact, null, completionMode);

    public static CollectTraceResult Failure(Session session, string error, TraceCompletionMode completionMode = TraceCompletionMode.Failed) =>
        new(session, null, error, completionMode);
}

public class CollectTraceService
{
    private readonly IOptions<ClrScopeOptions> _options;
    private readonly IPreflightValidator _preflightValidator;
    private readonly ISqliteSessionStore _sessionStore;
    private readonly ISqliteArtifactStore _artifactStore;
    private readonly IPidLockManager _pidLockManager;
    private readonly IActiveOperationRegistry _activeOperationRegistry;
    private readonly ILogger<CollectTraceService> _logger;

    public CollectTraceService(
        IOptions<ClrScopeOptions> options,
        IPreflightValidator preflightValidator,
        ISqliteSessionStore sessionStore,
        ISqliteArtifactStore artifactStore,
        IPidLockManager pidLockManager,
        IActiveOperationRegistry activeOperationRegistry,
        ILogger<CollectTraceService> logger)
    {
        _options = options;
        _preflightValidator = preflightValidator;
        _sessionStore = sessionStore;
        _artifactStore = artifactStore;
        _pidLockManager = pidLockManager;
        _activeOperationRegistry = activeOperationRegistry;
        _logger = logger;
    }

    public async Task<CollectTraceResult> CollectTraceAsync(
        CollectTraceRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0);
        _logger.LogInformation("[{Phase}] Starting trace collection for PID {Pid}", CollectionPhase.Preflight, request.Pid);

        // Acquire PID lock to serialize operations on the same process
        using var pidLock = await _pidLockManager.AcquireLockAsync(request.Pid, cancellationToken);

        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            _logger.LogError("[{Phase}] Preflight validation failed for PID {Pid}: {Error}", CollectionPhase.Preflight, request.Pid, preflightResult.Message);
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, request.Profile, cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = preflightResult.Message }, cancellationToken);
            return CollectTraceResult.Failure(failedSession, preflightResult.Message ?? "Preflight validation failed");
        }

        _logger.LogInformation("[{Phase}] Preflight validation passed for PID {Pid}", CollectionPhase.Preflight, request.Pid);

        // Parse duration (hh:mm:ss format)
        TimeSpan duration;
        try
        {
            duration = ParseDuration(request.Duration);
        }
        catch (FormatException ex)
        {
            _logger.LogError("[{Phase}] Invalid duration format: {Error}", CollectionPhase.Preflight, ex.Message);
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, request.Profile, cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = ex.Message }, cancellationToken);
            return CollectTraceResult.Failure(failedSession, $"Invalid duration format: {ex.Message}");
        }

        // Create session
        var session = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, request.Profile, cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Running }, cancellationToken);

        // Create linked CTS for operation cancellation
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeOperationRegistry.TryRegister(session.SessionId, operationCts);

        try
        {
            // Setup artifact path
        var artifactRoot = _options.Value.GetArtifactRoot();
        var tracesDir = Path.Combine(artifactRoot, "traces");
        Directory.CreateDirectory(tracesDir);

        var fileName = $"trace_{session.SessionId.Value}.nettrace";
        var filePath = Path.Combine(tracesDir, fileName);

        // Start EventPipeSession with bounded timeout
        progress?.Report(20);
        _logger.LogInformation("[{Phase}] Attaching to PID {Pid}", CollectionPhase.Attaching, request.Pid);
        Microsoft.Diagnostics.NETCore.Client.EventPipeSession? eventPipeSession = null;
        bool forced = false;
        TraceCompletionMode completionMode = TraceCompletionMode.Complete;

        try
        {
            using var startCts = CancellationTokenSource.CreateLinkedTokenSource(operationCts.Token);
            startCts.CancelAfter(TimeSpan.FromSeconds(30)); // Bounded timeout for start

            var client = new DiagnosticsClient(request.Pid);
            var providers = new List<EventPipeProvider>
            {
                new("Microsoft-Windows-DotNETRuntime", EventLevel.Informational)
            };

            eventPipeSession = await client.StartEventPipeSessionAsync(
                providers,
                requestRundown: true,
                circularBufferMB: 256,
                token: startCts.Token);
            _logger.LogInformation("[{Phase}] Successfully attached to PID {Pid}", CollectionPhase.Attaching, request.Pid);
        }
        catch (OperationCanceledException) when (!operationCts.Token.IsCancellationRequested)
        {
            _logger.LogError("[{Phase}] Attach timeout for PID {Pid}", CollectionPhase.Attaching, request.Pid);
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "StartEventPipeSession timed out" }, operationCts.Token);
            return CollectTraceResult.Failure(session, "StartEventPipeSession timed out");
        }

        // Copy EventPipeStream to file with FileMode.Create
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        try
        {
            progress?.Report(60);
            _logger.LogInformation("[{Phase}] Collecting trace for {Duration}", CollectionPhase.Collecting, duration);
            // copyTask not tied to operation cancellation - lifecycle managed by session.Stop/Dispose
            var copyTask = eventPipeSession.EventStream.CopyToAsync(fileStream, CancellationToken.None);

            var winner = await Task.WhenAny(copyTask, Task.Delay(duration, operationCts.Token));

            if (winner == copyTask)
            {
                completionMode = TraceCompletionMode.CompletedEarly;
                await copyTask; // Observe exception if any
            }
            else
            {
                // Graceful stop with bounded timeout
                try
                {
                    using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(operationCts.Token);
                    stopCts.CancelAfter(TimeSpan.FromSeconds(15)); // Bounded timeout for stop

                    await eventPipeSession.StopAsync(stopCts.Token);
                }
                catch (OperationCanceledException) when (!operationCts.Token.IsCancellationRequested)
                {
                    forced = true;
                    completionMode = TraceCompletionMode.Partial;
                    eventPipeSession.Dispose(); // Fallback
                }

                try
                {
                    await copyTask;
                }
                catch (Exception) when (forced)
                {
                    // Forced stop may interrupt stream - this is partial trace, not necessarily fatal
                    completionMode = TraceCompletionMode.Partial;
                }
            }

            // Flush with CancellationToken.None in cleanup
            await fileStream.FlushAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (operationCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("[{Phase}] Collection cancelled for PID {Pid}", CollectionPhase.Cancelled, request.Pid);
            eventPipeSession?.Dispose();
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Cancelled }, operationCts.Token);
            return CollectTraceResult.Failure(session, "Collection cancelled", TraceCompletionMode.Cancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Phase}] Collection failed for PID {Pid}", CollectionPhase.Failed, request.Pid);
            eventPipeSession?.Dispose();
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = ex.Message }, operationCts.Token);
            return CollectTraceResult.Failure(session, $"Failed to collect trace: {ex.Message}", TraceCompletionMode.Failed);
        }
        finally
        {
            eventPipeSession?.Dispose();
        }

        // Check if file was created
        if (!File.Exists(filePath))
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Trace file not created" }, operationCts.Token);
            return CollectTraceResult.Failure(session, "Trace file was not created", TraceCompletionMode.Failed);
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Trace file is empty" }, operationCts.Token);
            return CollectTraceResult.Failure(session, "Trace file is empty", TraceCompletionMode.Failed);
        }

        // Persist artifact
        progress?.Report(90);
        _logger.LogInformation("[{Phase}] Persisting artifact for session {SessionId}", CollectionPhase.Persisting, session.SessionId.Value);
        var artifact = await _artifactStore.CreateAsync(
            ArtifactKind.Trace,
            filePath,
            fileInfo.Length,
            request.Pid,
            session.SessionId,
            operationCts.Token
        );

        // Use the ArtifactId from store for URIs (fix double generation)
        var diagUri = $"clrscope://artifact/{artifact.ArtifactId.Value}";
        var fileUri = $"file://{filePath}";

        // Update artifact with URIs and status
        artifact = artifact with { DiagUri = diagUri, FileUri = fileUri };
        var artifactStatus = forced ? ArtifactStatus.Partial : ArtifactStatus.Completed;
        await _artifactStore.UpdateAsync(artifact with { Status = artifactStatus }, operationCts.Token);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Completed, CompletedAtUtc = DateTime.UtcNow }, operationCts.Token);

        progress?.Report(100);
        _logger.LogInformation("[{Phase}] Trace collection completed for PID {Pid}, SessionId {SessionId}, CompletionMode {CompletionMode}, Size {SizeBytes} bytes",
            CollectionPhase.Completed, request.Pid, session.SessionId.Value, completionMode, fileInfo.Length);

        return CollectTraceResult.Success(session, artifact, completionMode);
        }
        finally
        {
            _activeOperationRegistry.Complete(session.SessionId);
        }
    }

    private static TimeSpan ParseDuration(string duration)
    {
        // Parse hh:mm:ss format (3 parts: hours, minutes, seconds)
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
