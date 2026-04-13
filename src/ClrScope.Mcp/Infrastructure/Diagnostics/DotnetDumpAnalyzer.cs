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
    private readonly ICliToolAvailabilityChecker _availabilityChecker;

    public DotnetDumpAnalyzer(
        ILogger<DotnetDumpAnalyzer> logger,
        ICliCommandRunner cliRunner,
        CorrelationIdProvider correlationIdProvider,
        ICliToolAvailabilityChecker availabilityChecker)
    {
        _logger = logger;
        _cliRunner = cliRunner;
        _correlationIdProvider = correlationIdProvider;
        _availabilityChecker = availabilityChecker;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogInformation("[{CorrelationId}] Checking dotnet-dump availability", correlationId);

            // First check via availability checker
            var availability = await _availabilityChecker.CheckAvailabilityAsync("dotnet-dump", cancellationToken);
            if (!availability.IsAvailable)
            {
                return false;
            }

            // Additional check: try to run dotnet-dump --help to ensure it actually works
            var helpResult = await _cliRunner.ExecuteAsync("dotnet-dump", new[] { "--help" }, cancellationToken);
            if (helpResult.ExitCode != 0)
            {
                _logger.LogWarning("[{CorrelationId}] dotnet-dump found but --help failed with exit code {ExitCode}", correlationId, helpResult.ExitCode);
                return false;
            }

            return true;
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
            var args = new[] { "analyze", dumpFilePath, "-c", command };
            var commandResult = await _cliRunner.ExecuteAsync("dotnet-dump", args, cancellationToken);

            if (commandResult.ExitCode == 0)
            {
                _logger.LogInformation("[{CorrelationId}] SOS command executed successfully", correlationId);
                return SosAnalysisResult.SuccessResult(commandResult.StandardOutput);
            }
            else
            {
                var errorOutput = !string.IsNullOrEmpty(commandResult.StandardError) 
                    ? commandResult.StandardError 
                    : !string.IsNullOrEmpty(commandResult.StandardOutput) 
                        ? commandResult.StandardOutput 
                        : $"Exit code: {commandResult.ExitCode}";
                
                _logger.LogWarning("[{CorrelationId}] SOS command failed: {Error}", correlationId, errorOutput);
                return SosAnalysisResult.FailureResult($"SOS command failed: {errorOutput}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] SOS command execution failed", correlationId);
            return SosAnalysisResult.FailureResult($"SOS command execution failed: {ex.Message}");
        }
    }
}
