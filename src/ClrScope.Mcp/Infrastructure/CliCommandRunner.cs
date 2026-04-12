using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Реализация ICliCommandRunner через Process.Start.
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

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        var success = process.ExitCode == 0;

        if (!success)
        {
            _logger.LogWarning("Command failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
        }

        return new CommandLineResult(process.ExitCode, output, error, success);
    }
}
