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
using System.IO.Compression;

namespace ClrScope.Mcp.Services;

public record CollectDumpRequest(int Pid, bool IncludeHeap = true, bool Compress = false);

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
        _logger.LogInformation("[0%] Starting dump collection for PID {Pid}, IncludeHeap={IncludeHeap}, Compress={Compress}", request.Pid, request.IncludeHeap, request.Compress);

        // Acquire PID lock to serialize operations on the same process
        using var pidLock = await _pidLockManager.AcquireLockAsync(request.Pid, cancellationToken);

        // Preflight validation
        var preflightResult = await _preflightValidator.ValidateCollectAsync(request.Pid, cancellationToken);
        if (!preflightResult.IsValid)
        {
            var failedSession = await _sessionStore.CreateAsync(SessionKind.Dump, request.Pid, cancellationToken: cancellationToken);
            failedSession = failedSession.AsFailed(preflightResult.Message);
            await _sessionStore.UpdateAsync(failedSession, cancellationToken);
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

            var fileName = request.Compress ? $"dump_{session.SessionId.Value}.dmp.gz" : $"dump_{session.SessionId.Value}.dmp";
            filePath = Path.Combine(dumpsDir, fileName);

            // Write dump
            progress?.Report(20);
            _logger.LogInformation("[20%] Starting WriteDump for PID {Pid} to {FilePath}", request.Pid, filePath);
            await _sessionStore.UpdateAsync(session with { Phase = SessionPhase.Collecting }, operationCts.Token);
            try
            {
                var client = new DiagnosticsClient(request.Pid);
                var dumpType = request.IncludeHeap ? DumpType.WithHeap : DumpType.Normal;

                // If compression is requested, write to temporary file first, then compress
                var tempFilePath = request.Compress ? Path.Combine(dumpsDir, $"temp_{session.SessionId.Value}.dmp") : filePath;

                // WriteDump is synchronous and doesn't support cancellation from DiagnosticsClient API
                // Task.Run with cancellation token only prevents the task from starting if cancelled before execution
                // Once WriteDump starts, it cannot be cancelled - it will complete regardless of session.cancel
                await Task.Run(() =>
                {
                    client.WriteDump(dumpType, tempFilePath, false);
                }, operationCts.Token);
                _logger.LogInformation("[50%] WriteDump completed for PID {Pid}", request.Pid);

                // Compress the dump file if requested
                if (request.Compress)
                {
                    progress?.Report(50);
                    _logger.LogInformation("[50%] Compressing dump file for session {SessionId}", session.SessionId);
                    await Task.Run(() =>
                    {
                        using var originalFileStream = File.OpenRead(tempFilePath);
                        using var compressedFileStream = File.Create(filePath);
                        using var compressionStream = new GZipStream(compressedFileStream, CompressionLevel.Optimal);
                        originalFileStream.CopyTo(compressionStream);
                    }, operationCts.Token);

                    // Get sizes before deleting temp file
                    var originalSize = new FileInfo(tempFilePath).Length;
                    var compressedSize = new FileInfo(filePath).Length;
                    var compressionRatio = (1.0 - (double)compressedSize / originalSize) * 100;

                    // Delete the temporary uncompressed file
                    File.Delete(tempFilePath);

                    _logger.LogInformation("[60%] Dump compressed: {OriginalSize} -> {CompressedSize} ({CompressionRatio:F1}% reduction)",
                        FormatBytes(originalSize), FormatBytes(compressedSize), compressionRatio);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Dump collection cancellation requested for session {SessionId}, but WriteDump is synchronous and may still complete. Cancellation is best-effort only.", session.SessionId);
                session = session.AsCancelled();
                await _sessionStore.UpdateAsync(session, CancellationToken.None);
                return CollectDumpResult.Failure(session, "Dump collection cancellation requested (best-effort only - operation may still complete)");
            }
            catch (Exception ex)
            {
                session = session.AsFailed(ex.Message);
                await _sessionStore.UpdateAsync(session, CancellationToken.None);
                return CollectDumpResult.Failure(session, $"Failed to write dump: {ex.Message}");
            }

        // Check if file was created and has valid size
        if (!File.Exists(filePath))
            {
                session = session.AsFailed("Dump file not created");
                await _sessionStore.UpdateAsync(session, CancellationToken.None);
                return CollectDumpResult.Failure(session, "Dump file was not created");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                session = session.AsFailed("Dump file is empty");
                await _sessionStore.UpdateAsync(session, CancellationToken.None);
                return CollectDumpResult.Failure(session, "Dump file is empty");
            }

            // Create artifact record
            progress?.Report(70);
            _logger.LogInformation("[70%] Persisting artifact for session {SessionId}", session.SessionId);
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
            var fileUri = new Uri(filePath).AbsoluteUri;

            // Update artifact with URIs
            artifact = artifact with { DiagUri = diagUri, FileUri = fileUri };
            await _artifactStore.UpdateAsync(artifact with { Status = ArtifactStatus.Completed }, CancellationToken.None);
            await _sessionStore.UpdateAsync(session.AsCompleted(), CancellationToken.None);

            // Re-read to get updated state
            var updatedSession = await _sessionStore.GetAsync(session.SessionId, CancellationToken.None);
            var updatedArtifact = await _artifactStore.GetAsync(artifact.ArtifactId, CancellationToken.None);

            progress?.Report(100);
            _logger.LogInformation("[100%] Dump collection completed for PID {Pid}, SessionId {SessionId}, Size {Size}", request.Pid, session.SessionId.Value, FormatBytes(fileSize));
            return CollectDumpResult.Success(updatedSession ?? session, updatedArtifact ?? artifact);
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

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
