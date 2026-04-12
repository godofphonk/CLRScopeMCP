using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Symbol resolver implementation using dotnet-symbol CLI
/// </summary>
public class SymbolResolver : ISymbolResolver
{
    private readonly ILogger<SymbolResolver> _logger;
    private readonly ICliCommandRunner _cliRunner;
    private readonly CorrelationIdProvider _correlationIdProvider;
    private readonly IOptions<ClrScopeOptions> _options;

    public SymbolResolver(
        ILogger<SymbolResolver> logger,
        ICliCommandRunner cliRunner,
        CorrelationIdProvider correlationIdProvider,
        IOptions<ClrScopeOptions> options)
    {
        _logger = logger;
        _cliRunner = cliRunner;
        _correlationIdProvider = correlationIdProvider;
        _options = options;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogInformation("[{CorrelationId}] Checking dotnet-symbol availability", correlationId);

            var result = await _cliRunner.ExecuteAsync(
                "dotnet-symbol",
                new[] { "--version" },
                cancellationToken);

            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "dotnet-symbol not available");
            return false;
        }
    }

    public async Task<SymbolResolutionResult> ResolveAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        _logger.LogInformation("[{CorrelationId}] Resolving symbols for artifact {ArtifactId}", correlationId, artifactId);

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
                artifactId
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
