namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Checks availability of external CLI tools
/// </summary>
public interface ICliToolAvailabilityChecker
{
    /// <summary>
    /// Checks availability of a tool
    /// </summary>
    Task<CliToolAvailability> CheckAvailabilityAsync(string toolName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Synchronously checks availability of a tool (for use during DI configuration)
    /// </summary>
    CliToolAvailability CheckAvailabilitySync(string toolName);
}

/// <summary>
/// Result of CLI tool availability check
/// </summary>
public record CliToolAvailability(
    string ToolName,
    bool IsAvailable,
    string? Version = null,
    string? InstallHint = null
);
