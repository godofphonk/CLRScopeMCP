namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Interface for SOS analysis of dump files
/// </summary>
public interface ISosAnalyzer
{
    /// <summary>
    /// Execute SOS command on dump file
    /// </summary>
    /// <param name="dumpFilePath">Path to dump file</param>
    /// <param name="command">SOS command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of SOS command execution</returns>
    Task<SosAnalysisResult> ExecuteCommandAsync(
        string dumpFilePath,
        string command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if dotnet-sos is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of SOS analysis
/// </summary>
public record SosAnalysisResult(
    bool Success,
    string Output,
    string? Error
)
{
    public static SosAnalysisResult SuccessResult(string output) =>
        new(true, output, null);

    public static SosAnalysisResult FailureResult(string error) =>
        new(false, string.Empty, error);
}
