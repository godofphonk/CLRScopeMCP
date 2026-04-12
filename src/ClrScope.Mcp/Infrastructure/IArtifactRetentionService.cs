namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Service for managing artifact retention and cleanup
/// </summary>
public interface IArtifactRetentionService
{
    /// <summary>
    /// Clean up artifacts older than the specified age
    /// </summary>
    /// <param name="maxAge">Maximum age of artifacts to keep</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of artifacts deleted</returns>
    Task<int> CleanupOldArtifactsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get total size of all artifacts
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total size in bytes</returns>
    Task<long> GetTotalArtifactSizeAsync(CancellationToken cancellationToken = default);
}
