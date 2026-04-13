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

        _logger.LogInformation("Executing: {Command} {Arguments}", command, string.Join(" ", arguments));

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return new CommandLineResult(-1, "", "Failed to start process", false);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Command cancelled, killing process {Pid}", process.Id);
            
            // Kill the process and its children
            try
            {
                KillProcessTree(process.Id);
                process.Kill(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill process {Pid}", process.Id);
            }

            // Wait a bit for cleanup
            await Task.Delay(100, CancellationToken.None);
            
            return new CommandLineResult(-1, "", "Command cancelled", false);
        }

        var output = await outputTask;
        var error = await errorTask;

        var success = process.ExitCode == 0;

        if (!success)
        {
            _logger.LogWarning("Command failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
        }

        return new CommandLineResult(process.ExitCode, output, error, success);
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
