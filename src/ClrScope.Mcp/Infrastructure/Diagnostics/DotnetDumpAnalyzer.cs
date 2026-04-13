using ClrScope.Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// SOS analyzer implementation using dotnet-dump analyze CLI
/// </summary>
public class DotnetDumpAnalyzer : ISosAnalyzer
{
    private readonly ILogger<DotnetDumpAnalyzer> _logger;
    private readonly ICliCommandRunner _cliRunner;
    private readonly CorrelationIdProvider _correlationIdProvider;

    public DotnetDumpAnalyzer(
        ILogger<DotnetDumpAnalyzer> logger,
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
            _logger.LogInformation("[{CorrelationId}] Checking dotnet-dump availability", correlationId);

            var result = await _cliRunner.ExecuteAsync(
                "dotnet-dump",
                new[] { "--version" },
                cancellationToken);

            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "dotnet-dump not available");
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

            // Execute dotnet-dump analyze with the SOS command
            // dotnet-dump analyze opens interactive REPL, so we use echo to pipe the command
            var args = new[] { "analyze", dumpFilePath, "-c", command };
            var commandResult = await _cliRunner.ExecuteAsync("dotnet-dump", args, cancellationToken);

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
