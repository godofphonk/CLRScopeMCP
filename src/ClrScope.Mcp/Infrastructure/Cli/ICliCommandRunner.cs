namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Interface for executing CLI commands (dotnet-counters, dotnet-gcdump, dotnet-stack).
/// </summary>
public interface ICliCommandRunner
{
    /// <summary>
    /// Executes a CLI command and returns stdout.
    /// </summary>
    Task<CommandLineResult> ExecuteAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a CLI command with a timeout.
    /// </summary>
    Task<CommandLineResult> ExecuteAsync(
        string command,
        string[] arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Normalized error categories for CLI command failures.
/// </summary>
public enum CommandErrorCategory
{
    None,
    NotFound,
    PermissionDenied,
    Timeout,
    InvalidArguments,
    RuntimeError,
    Unknown
}

/// <summary>
/// Result of CLI command execution.
/// </summary>
public record CommandLineResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool Success,
    CommandErrorCategory ErrorCategory = CommandErrorCategory.None,
    string? ErrorCategoryDetails = null);
