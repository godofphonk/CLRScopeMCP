using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Domain;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Validation;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Options;

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
    private readonly IPreflightValidator _preflightValidator;
    private readonly ISqliteSessionStore _sessionStore;
    private readonly ISqliteArtifactStore _artifactStore;
    private readonly IPidLockManager _pidLockManager;

    public CollectDumpService(
        IOptions<ClrScopeOptions> options,
        IPreflightValidator preflightValidator,
        ISqliteSessionStore sessionStore,
        ISqliteArtifactStore artifactStore,
        IPidLockManager pidLockManager)
    {
        _options = options;
        _preflightValidator = preflightValidator;
        _sessionStore = sessionStore;
        _artifactStore = artifactStore;
        _pidLockManager = pidLockManager;
    }

    public async Task<CollectDumpResult> CollectDumpAsync(
        CollectDumpRequest request,
        CancellationToken cancellationToken = default)
    {
        // Acquire PID lock to serialize operations on the same process
        using var pidLock = await _pidLockManager.AcquireLockAsync(request.Pid, cancellationToken);

        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Dump, request.Pid, cancellationToken: cancellationToken);
            await _sessionStore.UpdateAsync(failedSession with { Status = SessionStatus.Failed, Error = preflightResult.Message }, cancellationToken);
            return CollectDumpResult.Failure(failedSession, preflightResult.Message ?? "Preflight validation failed");
        }

        // Create session
        var session = await _sessionStore.CreateAsync(SessionKind.Dump, request.Pid, cancellationToken: cancellationToken);
        await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Running }, cancellationToken);

        // Setup artifact path
        var artifactRoot = _options.Value.GetArtifactRoot();
        var dumpsDir = Path.Combine(artifactRoot, "dumps");
        Directory.CreateDirectory(dumpsDir);

        var fileName = $"dump_{session.SessionId.Value}.dmp";
        var filePath = Path.Combine(dumpsDir, fileName);

        // Write dump
        try
        {
            var client = new DiagnosticsClient(request.Pid);
            var dumpType = request.IncludeHeap ? DumpType.WithHeap : DumpType.Normal;

            // Use working signature: WriteDump(DumpType, path, bool)
            client.WriteDump(dumpType, filePath, false);
        }
        catch (Exception ex)
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = ex.Message }, cancellationToken);
            return CollectDumpResult.Failure(session, $"Failed to write dump: {ex.Message}");
        }

        // Check if file was created
        if (!File.Exists(filePath))
        {
            await _sessionStore.UpdateAsync(session with { Status = SessionStatus.Failed, Error = "Dump file not created" }, cancellationToken);
            return CollectDumpResult.Failure(session, "Dump file was not created");
        }

        // Create artifact record
        var fileSize = new FileInfo(filePath).Length;

        var artifact = await _artifactStore.CreateAsync(
            ArtifactKind.Dump,
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

        return CollectDumpResult.Success(session, artifact);
    }
}
