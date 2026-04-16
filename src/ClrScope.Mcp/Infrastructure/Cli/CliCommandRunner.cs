using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Implementation of ICliCommandRunner via Process.Start.
/// </summary>
public class CliCommandRunner : ICliCommandRunner
{
    private readonly ILogger<CliCommandRunner> _logger;

    public CliCommandRunner(ILogger<CliCommandRunner> logger)
    {
        _logger = logger;
    }

    public async Task<CommandLineResult> ExecuteAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(command, arguments, Timeout.InfiniteTimeSpan, cancellationToken);
    }

    public async Task<CommandLineResult> ExecuteAsync(
        string command,
        string[] arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var maskedArguments = MaskSensitiveArguments(arguments);
        _logger.LogDebug(
            "Executing command {Command} {Args}; timeout={Timeout}; cancellationRequested={Cancelled}",
            command,
            string.Join(" ", maskedArguments),
            timeout,
            cancellationToken.IsCancellationRequested);

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return new CommandLineResult(-1, "", "Failed to start process", false, CommandErrorCategory.StartFailure);
        }

        // Start reading streams immediately to avoid deadlocks
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested &&
                    timeoutCts.IsCancellationRequested)
                {
                    // This is a timeout, not external cancellation
                    _logger.LogWarning("Command timed out after {Timeout}, killing process {Pid}", timeout, process.Id);
                    TryKill(process);

                    try
                    {
                        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }

                    var stdout = await SafeAwait(stdoutTask).ConfigureAwait(false);
                    var stderr = await SafeAwait(stderrTask).ConfigureAwait(false);

                    return new CommandLineResult(
                        -1,
                        stdout,
                        string.IsNullOrWhiteSpace(stderr)
                            ? $"Command timed out after {timeout}."
                            : $"{stderr}{Environment.NewLine}Command timed out after {timeout}.",
                        false,
                        CommandErrorCategory.Timeout);
                }
            }

            var standardOutput = await SafeAwait(stdoutTask).ConfigureAwait(false);
            var standardError = await SafeAwait(stderrTask).ConfigureAwait(false);

            var success = process.ExitCode == 0;
            var errorCategory = CategorizeError(process.ExitCode, standardError);

            if (!success)
            {
                _logger.LogWarning("Command failed with exit code {ExitCode}, category {Category}: {Error}", process.ExitCode, errorCategory, standardError);
            }

            return new CommandLineResult(process.ExitCode, standardOutput, standardError, success, errorCategory);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // This is external cancellation, not timeout
            _logger.LogDebug(
                "Command cancelled. externalCancelled={ExternalCancelled}, processExited={HasExited}",
                true,
                process.HasExited);

            TryKill(process);

            throw; // Re-throw OperationCanceledException for proper cancellation handling
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command execution failed for {Command}", command);
            TryKill(process);

            return new CommandLineResult(
                -1,
                "",
                ex.Message,
                false,
                CommandErrorCategory.RuntimeError);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private static async Task<string> SafeAwait(Task<string> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string[] MaskSensitiveArguments(string[] arguments)
    {
        return arguments.Select(arg =>
        {
            if (arg.Contains('/') || arg.Contains('\\'))
            {
                if (arg.StartsWith("-") || arg.StartsWith("--"))
                {
                    var parts = arg.Split(' ', 2);
                    if (parts.Length == 2 && (parts[1].Contains('/') || parts[1].Contains('\\')))
                    {
                        return $"{parts[0]} [MASKED_PATH]";
                    }
                }
                else if (arg.StartsWith("/") || arg.Contains(":/") || (arg.Length > 1 && arg[1] == ':'))
                {
                    return "[MASKED_PATH]";
                }
            }

            if (arg.ToLowerInvariant().Contains("token") || arg.ToLowerInvariant().Contains("key") || arg.ToLowerInvariant().Contains("secret"))
            {
                return "[MASKED_CREDENTIAL]";
            }

            return arg;
        }).ToArray();
    }

    private CommandErrorCategory CategorizeError(int exitCode, string error)
    {
        var errorLower = error.ToLowerInvariant();

        if (exitCode == 127 || exitCode == 1 && errorLower.Contains("not found") || errorLower.Contains("no such file"))
        {
            return CommandErrorCategory.NotFound;
        }

        if (exitCode == 126 || errorLower.Contains("permission denied") || errorLower.Contains("access denied"))
        {
            return CommandErrorCategory.PermissionDenied;
        }

        if (errorLower.Contains("invalid argument") || errorLower.Contains("unrecognized"))
        {
            return CommandErrorCategory.InvalidArguments;
        }

        return CommandErrorCategory.ProcessError;
    }
}
