using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Validation;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.Diagnostics.Tracing;

namespace ClrScope.Mcp.Services;

public record CollectTraceRequest(int Pid, string Duration, string? Profile = null, string[]? CustomProviders = null);

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
        _logger.LogInformation("[{Phase}] Starting trace collection for PID {Pid}", SessionPhase.Preflight, request.Pid);

        // Acquire PID lock to serialize operations on the same process
        using var pidLock = await _pidLockManager.AcquireLockAsync(request.Pid, cancellationToken);

        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            _logger.LogError("[{Phase}] Preflight validation failed for PID {Pid}: {Error}", SessionPhase.Preflight, request.Pid, preflightResult.Message);
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, cancellationToken: cancellationToken);
            failedSession = failedSession.AsFailed(preflightResult.Message);
            await _sessionStore.UpdateAsync(failedSession, cancellationToken);
            return CollectTraceResult.Failure(failedSession, preflightResult.Message ?? "Preflight validation failed");
        }

        _logger.LogInformation("[{Phase}] Preflight validation passed for PID {Pid}", SessionPhase.Preflight, request.Pid);

        // Parse duration (hh:mm:ss format)
        TimeSpan duration;
        try
        {
            duration = TimeSpanParser.ParseDuration(request.Duration);
        }
        catch (FormatException ex)
        {
            _logger.LogError("[{Phase}] Invalid duration format: {Error}", SessionPhase.Preflight, ex.Message);
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, cancellationToken: cancellationToken);
            failedSession = failedSession.AsFailed(ex.Message);
            await _sessionStore.UpdateAsync(failedSession, cancellationToken);
            return CollectTraceResult.Failure(failedSession, $"Invalid duration format: {ex.Message}");
        }

        // Create session
        var session = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, cancellationToken: cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Running, Phase = SessionPhase.Attaching }, cancellationToken);

        // Create linked CTS
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeOperationRegistry.TryRegister(session.SessionId, operationCts);

        string? filePath = null;
        try
        {
            // Setup artifact path
            var artifactRoot = _options.Value.GetArtifactRoot();
            var tracesDir = Path.Combine(artifactRoot, "traces");
            Directory.CreateDirectory(tracesDir);

            var fileName = $"trace_{session.SessionId.Value}.nettrace";
            filePath = Path.Combine(tracesDir, fileName);

            // Start EventPipeSession with bounded timeout
            progress?.Report(20);
            _logger.LogInformation("[{Phase}] Attaching to PID {Pid}", SessionPhase.Attaching, request.Pid);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Collecting }, operationCts.Token);
            Microsoft.Diagnostics.NETCore.Client.EventPipeSession? eventPipeSession = null;
            bool forced = false;
            TraceCompletionMode completionMode = TraceCompletionMode.Complete;

        try
        {
            using var startCts = CancellationTokenSource.CreateLinkedTokenSource(operationCts.Token);
            startCts.CancelAfter(TimeSpan.FromSeconds(30)); // Bounded timeout for start

            var client = new DiagnosticsClient(request.Pid);
            var providers = GetProvidersForProfile(request.Profile, request.CustomProviders);
            var providerType = request.CustomProviders != null && request.CustomProviders.Length > 0 ? "custom" : (request.Profile ?? "default");
            _logger.LogInformation("[{Phase}] Using {ProviderType} profile with {ProviderCount} providers", SessionPhase.Attaching, providerType, providers.Count);

            eventPipeSession = await client.StartEventPipeSessionAsync(
                providers,
                requestRundown: true,
                circularBufferMB: 256,
                token: startCts.Token);
            _logger.LogInformation("[{Phase}] Successfully attached to PID {Pid}", SessionPhase.Attaching, request.Pid);
        }
        catch (OperationCanceledException) when (!operationCts.Token.IsCancellationRequested)
        {
            _logger.LogError("[{Phase}] Attach timeout for PID {Pid}", SessionPhase.Attaching, request.Pid);
            session = session.AsFailed("StartEventPipeSession timed out");
            await _sessionStore.UpdateAsync(session, CancellationToken.None);
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
            _logger.LogInformation("[{Phase}] Collecting trace for {Duration}", SessionPhase.Collecting, duration);
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
            _logger.LogWarning("[{Phase}] Collection cancelled for PID {Pid}", SessionPhase.Cancelled, request.Pid);
            eventPipeSession?.Dispose();
            session = session.AsCancelled();
            await _sessionStore.UpdateAsync(session, CancellationToken.None);
            return CollectTraceResult.Failure(session, "Collection cancelled", TraceCompletionMode.Cancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Phase}] Collection failed for PID {Pid}", SessionPhase.Failed, request.Pid);
            eventPipeSession?.Dispose();
            session = session.AsFailed(ex.Message);
            await _sessionStore.UpdateAsync(session, CancellationToken.None);
            return CollectTraceResult.Failure(session, $"Failed to collect trace: {ex.Message}", TraceCompletionMode.Failed);
        }
        finally
        {
            eventPipeSession?.Dispose();
        }

        // Check if file was created
        if (!File.Exists(filePath))
        {
            session = session.AsFailed("Trace file not created");
            await _sessionStore.UpdateAsync(session, CancellationToken.None);
            return CollectTraceResult.Failure(session, "Trace file was not created", TraceCompletionMode.Failed);
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            session = session.AsFailed("Trace file is empty");
            await _sessionStore.UpdateAsync(session, CancellationToken.None);
            return CollectTraceResult.Failure(session, "Trace file is empty", TraceCompletionMode.Failed);
        }

        // Persist artifact
        progress?.Report(90);
        await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Persisting }, operationCts.Token);
        _logger.LogInformation("[{Phase}] Persisting artifact for session {SessionId}", SessionPhase.Persisting, session.SessionId.Value);
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
        var fileUri = new Uri(filePath).AbsoluteUri;

        // Update artifact with URIs and status
        artifact = artifact with { DiagUri = diagUri, FileUri = fileUri };
        var artifactStatus = forced ? ArtifactStatus.Partial : ArtifactStatus.Completed;
        await _artifactStore.UpdateAsync(artifact with { Status = artifactStatus }, CancellationToken.None);
        await _sessionStore.UpdateAsync(session.AsCompleted(), CancellationToken.None);

        // Re-read to get updated state
        var updatedSession = await _sessionStore.GetAsync(session.SessionId, CancellationToken.None);
        var updatedArtifact = await _artifactStore.GetAsync(artifact.ArtifactId, CancellationToken.None);

        progress?.Report(100);
        _logger.LogInformation("[{Phase}] Trace collection completed for PID {Pid}, SessionId {SessionId}, CompletionMode {CompletionMode}, Size {SizeBytes} bytes",
            SessionPhase.Completed, request.Pid, session.SessionId.Value, completionMode, fileInfo.Length);

        return CollectTraceResult.Success(updatedSession ?? session, updatedArtifact ?? artifact, completionMode);
        }
        catch (Exception ex) when (!operationCts.Token.IsCancellationRequested)
        {
            // Best-effort: mark session as failed
            try
            {
                session = session.AsFailed(ex.Message);
                await _sessionStore.UpdateAsync(session, CancellationToken.None);
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

    private static List<EventPipeProvider> GetProvidersForProfile(string? profile, string[]? customProviders = null)
    {
        // If custom providers are specified, use them
        if (customProviders != null && customProviders.Length > 0)
        {
            return customProviders
                .Select(p => ParseCustomProvider(p))
                .Where(p => p != null)
                .Cast<EventPipeProvider>()
                .ToList();
        }

        // Map profile names to EventPipeProvider configurations
        // Based on dotnet-trace profiles: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace
        return profile?.ToLowerInvariant() switch
        {
            "cpu-sampling" => new List<EventPipeProvider>
            {
                new("Microsoft-Windows-DotNETRuntime", EventLevel.Informational, (long)0x00000001) // CPU sampling
            },
            "gc-heap" => new List<EventPipeProvider>
            {
                new("Microsoft-Windows-DotNETRuntime", EventLevel.Informational, (long)0x00000001 | 0x00000010) // CPU sampling + GC
            },
            _ => new List<EventPipeProvider>
            {
                new("Microsoft-Windows-DotNETRuntime", EventLevel.Informational) // Default: basic runtime events
            }
        };
    }

    private static EventPipeProvider? ParseCustomProvider(string providerString)
    {
        // Parse custom provider string in format: "ProviderName:Level:Keywords"
        // Example: "Microsoft-Windows-DotNETRuntime:Informational:0x00000001"
        var parts = providerString.Split(':');
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return null;
        }

        var providerName = parts[0];
        var level = parts.Length > 1 ? ParseEventLevel(parts[1]) : EventLevel.Informational;
        var keywords = parts.Length > 2 ? ParseKeywords(parts[2]) : 0;

        return new EventPipeProvider(providerName, level, keywords);
    }

    private static EventLevel ParseEventLevel(string levelString)
    {
        return levelString.ToLowerInvariant() switch
        {
            "critical" => EventLevel.Critical,
            "error" => EventLevel.Error,
            "warning" => EventLevel.Warning,
            "informational" => EventLevel.Informational,
            "verbose" => EventLevel.Verbose,
            "logalways" => EventLevel.LogAlways,
            _ => EventLevel.Informational
        };
    }

    private static long ParseKeywords(string keywordString)
    {
        if (keywordString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt64(keywordString, 16);
        }
        return long.TryParse(keywordString, out var result) ? result : 0;
    }
}
