namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Interface for collecting performance counters
/// </summary>
public interface ICountersBackend
{
    /// <summary>
    /// Collect performance counters from a process
    /// </summary>
    /// <param name="pid">Process ID</param>
    /// <param name="providers">Counter providers to collect</param>
    /// <param name="duration">Duration to collect</param>
    /// <param name="outputPath">Path to save counter data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of counter collection</returns>
    Task<CountersResult> CollectAsync(
        int pid,
        string[] providers,
        TimeSpan duration,
        string outputPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of counter collection
/// </summary>
public record CountersResult(
    bool Success,
    string? ArtifactId,
    string? Error,
    int EventCount = 0
)
{
    public static CountersResult SuccessResult(string artifactId, int eventCount) =>
        new(true, artifactId, null, eventCount);

    public static CountersResult FailureResult(string error) =>
        new(false, null, error);
}
