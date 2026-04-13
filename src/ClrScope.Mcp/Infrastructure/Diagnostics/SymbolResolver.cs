using ClrScope.Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Symbol resolver implementation using dotnet-symbol CLI
/// </summary>
public class SymbolResolver : ISymbolResolver
{
    private readonly ILogger<SymbolResolver> _logger;
    private readonly ICliCommandRunner _cliRunner;
    private readonly CorrelationIdProvider _correlationIdProvider;
    private readonly ICliToolAvailabilityChecker _availabilityChecker;

    public SymbolResolver(
        ILogger<SymbolResolver> logger,
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
            _logger.LogInformation("[{CorrelationId}] Checking dotnet-symbol availability", correlationId);

            // First check via availability checker
            var availability = await _availabilityChecker.CheckAvailabilityAsync("dotnet-symbol", cancellationToken);
            if (!availability.IsAvailable)
            {
                return false;
            }

            // Additional check: try to run dotnet-symbol --help to ensure it actually works
            var helpResult = await _cliRunner.ExecuteAsync("dotnet-symbol", new[] { "--help" }, cancellationToken);
            if (helpResult.ExitCode != 0)
            {
                _logger.LogWarning("[{CorrelationId}] dotnet-symbol found but --help failed with exit code {ExitCode}", correlationId, helpResult.ExitCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "dotnet-symbol not available");
            return false;
        }
    }

    public async Task<SymbolResolutionResult> ResolveAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        _logger.LogInformation("[{CorrelationId}] Resolving symbols for artifact {FilePath}", correlationId, filePath);

        try
        {
            // Get symbol cache directory
            var symbolCache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".clrscope",
                "symbols");

            Directory.CreateDirectory(symbolCache);

            // Execute dotnet-symbol command
            var args = new[]
            {
                "--symbols",
                "--output", symbolCache,
                filePath
            };

            var result = await _cliRunner.ExecuteAsync("dotnet-symbol", args, cancellationToken);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("[{CorrelationId}] Symbols resolved successfully to {SymbolCache}", correlationId, symbolCache);
                return SymbolResolutionResult.SuccessResult(symbolCache);
            }
            else
            {
                _logger.LogWarning("[{CorrelationId}] Symbol resolution failed: {Error}", correlationId, result.StandardError);
                return SymbolResolutionResult.FailureResult($"Symbol resolution failed: {result.StandardError}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Symbol resolution failed", correlationId);
            return SymbolResolutionResult.FailureResult($"Symbol resolution failed: {ex.Message}");
        }
    }
}
