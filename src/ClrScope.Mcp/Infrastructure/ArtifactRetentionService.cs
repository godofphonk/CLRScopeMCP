using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Service for managing artifact retention and cleanup
/// </summary>
public class ArtifactRetentionService : IArtifactRetentionService
{
    private readonly ISqliteArtifactStore _artifactStore;
    private readonly ILogger<ArtifactRetentionService> _logger;

    public ArtifactRetentionService(
        ISqliteArtifactStore artifactStore,
        ILogger<ArtifactRetentionService> logger)
    {
        _artifactStore = artifactStore;
        _logger = logger;
    }

    public async Task<int> CleanupOldArtifactsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        return await CleanupOldArtifactsAsync(maxAge, null, cancellationToken);
    }

    public async Task<int> CleanupOldArtifactsAsync(TimeSpan maxAge, long? maxTotalSizeBytes, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        var artifacts = await _artifactStore.GetAllAsync(cancellationToken);

        // Filter by age and pin status
        var candidates = artifacts
            .Where(a => a.CreatedAtUtc < cutoffTime && !a.Pinned)
            .OrderBy(a => a.CreatedAtUtc)
            .ToList();

        var deletedCount = 0;
        long totalDeletedSize = 0;

        foreach (var artifact in candidates)
        {
            // Check if we've exceeded max total size (if specified)
            if (maxTotalSizeBytes.HasValue && totalDeletedSize >= maxTotalSizeBytes.Value)
            {
                _logger.LogInformation("Reached max total size limit {MaxSizeBytes}, stopping cleanup", maxTotalSizeBytes.Value);
                break;
            }

            try
            {
                // Delete file if it exists
                if (File.Exists(artifact.FilePath))
                {
                    File.Delete(artifact.FilePath);
                    _logger.LogInformation("Deleted artifact file {FilePath}", artifact.FilePath);
                }

                // Delete database record
                await _artifactStore.DeleteAsync(artifact.ArtifactId, cancellationToken);
                deletedCount++;
                totalDeletedSize += artifact.SizeBytes;
                _logger.LogInformation("Deleted artifact {ArtifactId} created at {CreatedAt} (size: {SizeBytes})",
                    artifact.ArtifactId.Value, artifact.CreatedAtUtc, artifact.SizeBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete artifact {ArtifactId}", artifact.ArtifactId.Value);
            }
        }

        _logger.LogInformation("Cleanup completed: {DeletedCount} artifacts deleted, total size: {TotalSize} bytes",
            deletedCount, totalDeletedSize);
        return deletedCount;
    }

    public async Task<long> GetTotalArtifactSizeAsync(CancellationToken cancellationToken = default)
    {
        var artifacts = await _artifactStore.GetAllAsync(cancellationToken);
        var totalSize = artifacts.Sum(a => a.SizeBytes);
        _logger.LogInformation("Total artifact size: {TotalSize} bytes ({TotalMB} MB)",
            totalSize, totalSize / (1024 * 1024));
        return totalSize;
    }
}
