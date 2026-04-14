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
        _logger.LogInformation("Executing: {Command} {Arguments}", command, string.Join(" ", maskedArguments));

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
            return new CommandLineResult(-1, "", "Failed to start process", false, CommandErrorCategory.RuntimeError);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            var timeoutTask = timeout == Timeout.InfiniteTimeSpan
                ? Task.CompletedTask
                : Task.Delay(timeout, cancellationToken);

            var exitTask = process.WaitForExitAsync(cancellationToken);

            var completedTask = await Task.WhenAny(exitTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Command timed out after {Timeout}, killing process {Pid}", timeout, process.Id);
                KillProcessTree(process.Id);
                process.Kill(true);
                await Task.Delay(100, CancellationToken.None);
                return new CommandLineResult(-1, "", $"Command timed out after {timeout}", false, CommandErrorCategory.Timeout);
            }

            await exitTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Command cancelled, killing process {Pid}", process.Id);

            try
            {
                KillProcessTree(process.Id);
                process.Kill(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill process {Pid}", process.Id);
            }

            await Task.Delay(100, CancellationToken.None);
            throw;
        }

        var output = await outputTask;
        var error = await errorTask;

        var success = process.ExitCode == 0;
        var errorCategory = CategorizeError(process.ExitCode, error);

        if (!success)
        {
            _logger.LogWarning("Command failed with exit code {ExitCode}, category {Category}: {Error}", process.ExitCode, errorCategory, error);
        }

        return new CommandLineResult(process.ExitCode, output, error, success, errorCategory);
    }

    private string[] MaskSensitiveArguments(string[] arguments)
    {
        // Mask file paths and potentially sensitive arguments
        return arguments.Select(arg =>
        {
            // Mask file paths (arguments that look like paths)
            if (arg.Contains('/') || arg.Contains('\\'))
            {
                if (arg.StartsWith("-") || arg.StartsWith("--"))
                {
                    // It's a flag with a path value like "-o /path/to/file"
                    var parts = arg.Split(' ', 2);
                    if (parts.Length == 2 && (parts[1].Contains('/') || parts[1].Contains('\\')))
                    {
                        return $"{parts[0]} [MASKED_PATH]";
                    }
                }
                else if (arg.StartsWith("/") || arg.Contains(":/") || (arg.Length > 1 && arg[1] == ':'))
                {
                    // It's a path
                    return "[MASKED_PATH]";
                }
            }

            // Mask token-like arguments (commonly used for auth)
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

        if (exitCode == 124 || errorLower.Contains("timeout") || errorLower.Contains("timed out"))
        {
            return CommandErrorCategory.Timeout;
        }

        return CommandErrorCategory.RuntimeError;
    }

    private void KillProcessTree(int pid)
    {
        try
        {
            // On Unix, use pkill to kill process tree
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                var killProcess = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "pkill",
                        ArgumentList = { "-P", pid.ToString() },
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                killProcess?.WaitForExit();
            }
            else
            {
                // On Windows, taskkill to kill process tree
                var killProcess = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        ArgumentList = { "/F", "/T", "/PID", pid.ToString() },
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                killProcess?.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill process tree for {Pid}", pid);
        }
    }
}
