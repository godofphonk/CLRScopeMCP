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
    string? Error
)
{
    public static CollectTraceResult Success(Session session, Artifact artifact) =>
        new(session, artifact, null);

    public static CollectTraceResult Failure(Session session, string error) =>
        new(session, null, error);
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

        // Parse duration (dd:hh:mm:ss format per PC5 verification)
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

        // Start EventPipeSession
        Microsoft.Diagnostics.NETCore.Client.EventPipeSession? eventPipeSession = null;
        FileStream? fileStream = null;
        try
        {
            var client = new DiagnosticsClient(request.Pid);
            var providers = new List<EventPipeProvider>
            {
                new("Microsoft-Windows-DotNETRuntime", EventLevel.Informational)
            };

            eventPipeSession = client.StartEventPipeSession(providers, requestRundown: true, circularBufferMB: 256);

            // Copy EventPipeStream to file
            fileStream = File.OpenWrite(filePath);
            var copyTask = eventPipeSession.EventStream.CopyToAsync(fileStream, cancellationToken);

            // Wait for duration or cancellation (workaround for PC2: do NOT call session.Stop())
            await Task.WhenAny(copyTask, Task.Delay(duration, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested
            if (fileStream != null)
            {
                await fileStream.FlushAsync(cancellationToken);
                fileStream.Dispose();
            }
            eventPipeSession?.Dispose();
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Cancelled }, cancellationToken);
            return CollectTraceResult.Failure(session, "Collection cancelled");
        }
        catch (Exception ex)
        {
            fileStream?.Dispose();
            eventPipeSession?.Dispose();
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = ex.Message }, cancellationToken);
            return CollectTraceResult.Failure(session, $"Failed to collect trace: {ex.Message}");
        }

        // Check if file was created
        if (!File.Exists(filePath))
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Trace file not created" }, cancellationToken);
            return CollectTraceResult.Failure(session, "Trace file was not created");
        }

        // Create artifact record
        var fileSize = new FileInfo(filePath).Length;
        
        var artifact = await _artifactStore.CreateAsync(
            ArtifactKind.Trace,
            filePath,
            fileSize,
            request.Pid,
            session.SessionId,
            cancellationToken
        );

        // Use the ArtifactId from store for URIs (fix double generation)
        var diagUri = $"clrscope://artifact/{artifact.ArtifactId.Value}";
        var fileUri = $"file://{filePath}";

        // Update artifact with URIs
        artifact = artifact with { DiagUri = diagUri, FileUri = fileUri };
        await _artifactStore.UpdateAsync(artifact with { Status = ArtifactStatus.Completed }, cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Completed, CompletedAtUtc = DateTime.UtcNow }, cancellationToken);

        // Cleanup
        if (fileStream != null)
        {
            await fileStream.FlushAsync(cancellationToken);
            fileStream.Dispose();
        }
        eventPipeSession?.Dispose();

        return CollectTraceResult.Success(session, artifact);
    }

    private static TimeSpan ParseDuration(string duration)
    {
        // Parse dd:hh:mm format (3 parts: days, hours, minutes)
        var parts = duration.Split(':');
        if (parts.Length != 3)
        {
            throw new FormatException("Duration must be in dd:hh:mm format");
        }

        var days = int.Parse(parts[0]);
        var hours = int.Parse(parts[1]);
        var minutes = int.Parse(parts[2]);

        return new TimeSpan(days, hours, minutes, 0);
    }
}
