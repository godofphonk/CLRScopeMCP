namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Interface for dotnet-monitor integration
/// </summary>
public interface IMonitorBackend
{
    /// <summary>
    /// Check if dotnet-monitor is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start trace capture via dotnet-monitor
    /// </summary>
    /// <param name="pid">Process ID</param>
    /// <param name="duration">Duration to capture</param>
    /// <param name="outputPath">Path to save trace</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of trace capture</returns>
    Task<MonitorResult> StartTraceAsync(
        int pid,
        TimeSpan duration,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture dump via dotnet-monitor
    /// </summary>
    /// <param name="pid">Process ID</param>
    /// <param name="includeHeap">Include heap in dump</param>
    /// <param name="outputPath">Path to save dump</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of dump capture</returns>
    Task<MonitorResult> CaptureDumpAsync(
        int pid,
        bool includeHeap,
        string outputPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of monitor operation
/// </summary>
public record MonitorResult(
    bool Success,
    string? ArtifactId,
    string? Error
)
{
    public static MonitorResult SuccessResult(string artifactId) =>
        new(true, artifactId, null);

    public static MonitorResult FailureResult(string error) =>
        new(false, null, error);
}
