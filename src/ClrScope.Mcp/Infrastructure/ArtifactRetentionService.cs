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
        var cutoffTime = DateTime.UtcNow - maxAge;
        var artifacts = await _artifactStore.GetAllAsync(cancellationToken);

        var deletedCount = 0;
        foreach (var artifact in artifacts)
        {
            if (artifact.CreatedAtUtc < cutoffTime)
            {
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
                    _logger.LogInformation("Deleted artifact {ArtifactId} created at {CreatedAt}",
                        artifact.ArtifactId.Value, artifact.CreatedAtUtc);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete artifact {ArtifactId}", artifact.ArtifactId.Value);
                }
            }
        }

        _logger.LogInformation("Cleanup completed: {DeletedCount} artifacts deleted", deletedCount);
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
