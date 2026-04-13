using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Service for managing artifact retention and cleanup
/// </summary>
public class ArtifactRetentionService : IArtifactRetentionService
{
    private readonly ISqliteArtifactStore _artifactStore;
    private readonly ILogger<ArtifactRetentionService> _logger;
    private readonly IOptions<ClrScopeOptions> _options;

    public ArtifactRetentionService(
        ISqliteArtifactStore artifactStore,
        ILogger<ArtifactRetentionService> logger,
        IOptions<ClrScopeOptions> options)
    {
        _artifactStore = artifactStore;
        _logger = logger;
        _options = options;
    }

    public async Task<int> CleanupOldArtifactsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        return await CleanupOldArtifactsAsync(maxAge, null, cancellationToken);
    }

    public async Task<int> CleanupOldArtifactsAsync(TimeSpan maxAge, long? maxTotalSizeBytes, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        var artifacts = await _artifactStore.GetAllAsync(cancellationToken);
        var artifactRoot = _options.Value.GetArtifactRoot();

        // Filter by age and pin status
        var candidates = artifacts
            .Where(a => a.CreatedAtUtc < cutoffTime && !a.Pinned)
            .OrderBy(a => a.CreatedAtUtc)
            .ToList();

        var deletedCount = 0;
        long totalDeletedSize = 0;

        foreach (var artifact in candidates)
        {
            // Check if we would exceed max total size after deleting this artifact (if specified)
            if (maxTotalSizeBytes.HasValue && totalDeletedSize + artifact.SizeBytes > maxTotalSizeBytes.Value)
            {
                _logger.LogInformation("Reached max total size limit {MaxSizeBytes}, stopping cleanup", maxTotalSizeBytes.Value);
                break;
            }

            try
            {
                // Validate that file path is within artifact root before deletion
                if (!PathSecurity.IsPathWithinDirectory(artifact.FilePath, artifactRoot))
                {
                    _logger.LogWarning(
                        "Skipping artifact {ArtifactId} - file path {FilePath} is outside artifact root {ArtifactRoot}. This may indicate data corruption or security issue.",
                        artifact.ArtifactId.Value, artifact.FilePath, artifactRoot);
                    // Delete database record but skip file deletion for security
                    await _artifactStore.DeleteAsync(artifact.ArtifactId, cancellationToken);
                    continue;
                }

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
