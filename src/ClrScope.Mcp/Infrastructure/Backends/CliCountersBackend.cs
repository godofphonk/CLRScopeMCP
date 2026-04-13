using ClrScope.Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// CLI backend for collecting performance counters using dotnet-counters
/// </summary>
public class CliCountersBackend : ICountersBackend
{
    private readonly ICliCommandRunner _cliRunner;
    private readonly ICliToolAvailabilityChecker _availabilityChecker;
    private readonly ILogger<CliCountersBackend> _logger;

    public CliCountersBackend(
        ICliCommandRunner cliRunner,
        ICliToolAvailabilityChecker availabilityChecker,
        ILogger<CliCountersBackend> logger)
    {
        _cliRunner = cliRunner;
        _availabilityChecker = availabilityChecker;
        _logger = logger;
    }

    public async Task<CountersResult> CollectAsync(
        int pid,
        string[] providers,
        TimeSpan duration,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check dotnet-counters availability
            var availability = await _availabilityChecker.CheckAvailabilityAsync("dotnet-counters", cancellationToken);
            if (!availability.IsAvailable)
            {
                return CountersResult.FailureResult($"dotnet-counters CLI not found. {availability.InstallHint}");
            }

            _logger.LogInformation("Starting counters collection for PID {Pid} using dotnet-counters CLI", pid);

            // Build counters argument
            var countersArg = string.Join(",", providers);

            // Format duration as dd:hh:mm:ss
            var durationArg = FormatDuration(duration);

            // Build dotnet-counters collect command
            var args = new[]
            {
                "collect",
                "-p", pid.ToString(),
                "--format", "csv",
                "-o", outputPath,
                "--duration", durationArg,
                "--counters", countersArg
            };

            _logger.LogInformation("Executing: dotnet-counters {Args}", string.Join(" ", args));

            // Execute dotnet-counters
            var result = await _cliRunner.ExecuteAsync("dotnet-counters", args, cancellationToken);

            if (result.ExitCode != 0)
            {
                var error = !string.IsNullOrEmpty(result.StandardError) 
                    ? result.StandardError 
                    : result.StandardOutput;
                return CountersResult.FailureResult($"dotnet-counters failed: {error}");
            }

            // Verify file was created
            if (!File.Exists(outputPath))
            {
                return CountersResult.FailureResult("Counters file was not created");
            }

            var fileInfo = new FileInfo(outputPath);
            if (fileInfo.Length == 0)
            {
                return CountersResult.FailureResult("Counters file is empty");
            }

            _logger.LogInformation("Counters collection completed successfully, file size: {Size} bytes", fileInfo.Length);

            return CountersResult.SuccessResult(outputPath, 0);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Counters collection cancelled");
            throw; // Re-throw to allow upper layer to handle cancellation properly
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Counters collection failed for PID {Pid}", pid);
            return CountersResult.FailureResult($"Counters collection failed: {ex.Message}");
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        // Format as dd:hh:mm:ss
        var days = duration.Days;
        var hours = duration.Hours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;

        return $"{days:D2}:{hours:D2}:{minutes:D2}:{seconds:D2}";
    }
}
