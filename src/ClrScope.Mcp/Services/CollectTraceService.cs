using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Domain;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Validation;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Options;
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

    public CollectTraceService(
        IOptions<ClrScopeOptions> options,
        IPreflightValidator preflightValidator,
        ISqliteSessionStore sessionStore,
        ISqliteArtifactStore artifactStore)
    {
        _options = options;
        _preflightValidator = preflightValidator;
        _sessionStore = sessionStore;
        _artifactStore = artifactStore;
    }

    public async Task<CollectTraceResult> CollectTraceAsync(
        CollectTraceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, request.Profile, cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = preflightResult.Message }, cancellationToken);
            return CollectTraceResult.Failure(failedSession, preflightResult.Message ?? "Preflight validation failed");
        }

        // Parse duration (hh:mm:ss format)
        TimeSpan duration;
        try
        {
            duration = ParseDuration(request.Duration);
        }
        catch (FormatException ex)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, request.Profile, cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = ex.Message }, cancellationToken);
            return CollectTraceResult.Failure(failedSession, $"Invalid duration format: {ex.Message}");
        }

        // Create session
        var session = await _sessionStore.CreateAsync(SessionKind.Trace, request.Pid, request.Profile, cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Running }, cancellationToken);

        // Setup artifact path
        var artifactRoot = _options.Value.GetArtifactRoot();
        var tracesDir = Path.Combine(artifactRoot, "traces");
        Directory.CreateDirectory(tracesDir);

        var fileName = $"trace_{session.SessionId.Value}.nettrace";
        var filePath = Path.Combine(tracesDir, fileName);

        // Start EventPipeSession with bounded timeout
        Microsoft.Diagnostics.NETCore.Client.EventPipeSession? eventPipeSession = null;
        bool forced = false;
        TraceCompletionMode completionMode = TraceCompletionMode.Complete;

        try
        {
            using var startCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "StartEventPipeSession timed out" }, cancellationToken);
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
            // copyTask not tied to operation cancellation - lifecycle managed by session.Stop/Dispose
            var copyTask = eventPipeSession.EventStream.CopyToAsync(fileStream, CancellationToken.None);

            var winner = await Task.WhenAny(copyTask, Task.Delay(duration, cancellationToken));

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
                    using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    stopCts.CancelAfter(TimeSpan.FromSeconds(15)); // Bounded timeout for stop

                    await eventPipeSession.StopAsync(stopCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            eventPipeSession?.Dispose();
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Cancelled }, cancellationToken);
            return CollectTraceResult.Failure(session, "Collection cancelled", TraceCompletionMode.Cancelled);
        }
        catch (Exception ex)
        {
            eventPipeSession?.Dispose();
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = ex.Message }, cancellationToken);
            return CollectTraceResult.Failure(session, $"Failed to collect trace: {ex.Message}", TraceCompletionMode.Failed);
        }
        finally
        {
            eventPipeSession?.Dispose();
        }

        // Check if file was created
        if (!File.Exists(filePath))
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Trace file not created" }, cancellationToken);
            return CollectTraceResult.Failure(session, "Trace file was not created", TraceCompletionMode.Failed);
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Trace file is empty" }, cancellationToken);
            return CollectTraceResult.Failure(session, "Trace file is empty", TraceCompletionMode.Failed);
        }

        // Create artifact record
        var artifact = await _artifactStore.CreateAsync(
            ArtifactKind.Trace,
            filePath,
            fileInfo.Length,
            request.Pid,
            session.SessionId,
            cancellationToken
        );

        // Use the ArtifactId from store for URIs (fix double generation)
        var diagUri = $"clrscope://artifact/{artifact.ArtifactId.Value}";
        var fileUri = $"file://{filePath}";

        // Update artifact with URIs and status
        artifact = artifact with { DiagUri = diagUri, FileUri = fileUri };
        var artifactStatus = forced ? ArtifactStatus.Partial : ArtifactStatus.Completed;
        await _artifactStore.UpdateAsync(artifact with { Status = artifactStatus }, cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Completed, CompletedAtUtc = DateTime.UtcNow }, cancellationToken);

        return CollectTraceResult.Success(session, artifact, completionMode);
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
