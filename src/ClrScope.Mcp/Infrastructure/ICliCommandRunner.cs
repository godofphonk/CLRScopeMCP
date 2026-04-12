namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Интерфейс для выполнения CLI команд (dotnet-counters, dotnet-gcdump, dotnet-stack).
/// </summary>
public interface ICliCommandRunner
{
    /// <summary>
    /// Выполняет CLI команду и возвращает stdout.
    /// </summary>
    Task<CommandLineResult> ExecuteAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат выполнения CLI команды.
/// </summary>
public record CommandLineResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool Success);
