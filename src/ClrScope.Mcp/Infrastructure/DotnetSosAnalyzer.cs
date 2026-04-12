using ClrScope.Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// SOS analyzer implementation using dotnet-sos CLI
/// </summary>
public class DotnetSosAnalyzer : ISosAnalyzer
{
    private readonly ILogger<DotnetSosAnalyzer> _logger;
    private readonly ICliCommandRunner _cliRunner;
    private readonly CorrelationIdProvider _correlationIdProvider;

    public DotnetSosAnalyzer(
        ILogger<DotnetSosAnalyzer> logger,
        ICliCommandRunner cliRunner,
        CorrelationIdProvider correlationIdProvider)
    {
        _logger = logger;
        _cliRunner = cliRunner;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogInformation("[{CorrelationId}] Checking dotnet-sos availability", correlationId);

            var result = await _cliRunner.ExecuteAsync(
                "dotnet-sos",
                new[] { "--version" },
                cancellationToken);

            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "dotnet-sos not available");
            return false;
        }
    }

    public async Task<SosAnalysisResult> ExecuteCommandAsync(
        string dumpFilePath,
        string command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        _logger.LogInformation("[{CorrelationId}] Executing SOS command '{Command}' on dump {DumpPath}", correlationId, command, dumpFilePath);

        try
        {
            // Check if dump file exists
            if (!File.Exists(dumpFilePath))
            {
                return SosAnalysisResult.FailureResult($"Dump file not found: {dumpFilePath}");
            }

            // Execute dotnet-sos command
            // dotnet-sos requires loading the dump first, then executing commands
            var args = new[] { "load", dumpFilePath };
            var loadResult = await _cliRunner.ExecuteAsync("dotnet-sos", args, cancellationToken);

            if (loadResult.ExitCode != 0)
            {
                return SosAnalysisResult.FailureResult($"Failed to load dump: {loadResult.StandardError}");
            }

            // Execute the SOS command
            var commandArgs = new[] { command };
            var commandResult = await _cliRunner.ExecuteAsync("dotnet-sos", commandArgs, cancellationToken);

            if (commandResult.ExitCode == 0)
            {
                _logger.LogInformation("[{CorrelationId}] SOS command executed successfully", correlationId);
                return SosAnalysisResult.SuccessResult(commandResult.StandardOutput);
            }
            else
            {
                _logger.LogWarning("[{CorrelationId}] SOS command failed: {Error}", correlationId, commandResult.StandardError);
                return SosAnalysisResult.FailureResult($"SOS command failed: {commandResult.StandardError}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] SOS command execution failed", correlationId);
            return SosAnalysisResult.FailureResult($"SOS command execution failed: {ex.Message}");
        }
    }
}
