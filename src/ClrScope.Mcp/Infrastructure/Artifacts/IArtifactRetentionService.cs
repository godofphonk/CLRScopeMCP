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
    /// Clean up artifacts older than the specified age and/or limit total size
    /// </summary>
    /// <param name="maxAge">Maximum age of artifacts to keep</param>
    /// <param name="maxTotalSizeBytes">Maximum total size to delete (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of artifacts deleted</returns>
    Task<int> CleanupOldArtifactsAsync(TimeSpan maxAge, long? maxTotalSizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up artifacts using specified strategy
    /// </summary>
    /// <param name="maxAge">Maximum age of artifacts to keep</param>
    /// <param name="maxTotalSizeBytes">Maximum total size to delete (optional)</param>
    /// <param name="strategy">Cleanup strategy: age (default), duplicates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of artifacts deleted</returns>
    Task<int> CleanupOldArtifactsAsync(TimeSpan maxAge, long? maxTotalSizeBytes, string strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get total size of all artifacts
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total size in bytes</returns>
    Task<long> GetTotalArtifactSizeAsync(CancellationToken cancellationToken = default);
}
