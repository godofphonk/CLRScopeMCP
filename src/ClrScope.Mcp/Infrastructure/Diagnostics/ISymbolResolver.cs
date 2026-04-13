namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Interface for symbol resolution
/// </summary>
public interface ISymbolResolver
{
    /// <summary>
    /// Resolve symbols for an artifact
    /// </summary>
    /// <param name="filePath">Artifact file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of symbol resolution</returns>
    Task<SymbolResolutionResult> ResolveAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if dotnet-symbol is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of symbol resolution
/// </summary>
public record SymbolResolutionResult(
    bool Success,
    string SymbolPath,
    string? Error
)
{
    public static SymbolResolutionResult SuccessResult(string symbolPath) =>
        new(true, symbolPath, null);

    public static SymbolResolutionResult FailureResult(string error) =>
        new(false, string.Empty, error);
}
