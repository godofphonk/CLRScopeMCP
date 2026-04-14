using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ClrScope.Mcp.Tools.Analysis;

[McpServerToolType]
public sealed class AnalysisTools
{
    [McpServerTool(Name = "analyze_dump_sos")]
    public static async Task<AnalyzeDumpSosResult> AnalyzeDumpSos(
        string artifactId,
        string command,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var sosAnalyzer = server.Services!.GetRequiredService<ISosAnalyzer>();
        var logger = server.Services!.GetRequiredService<ILogger<AnalysisTools>>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();

        if (string.IsNullOrEmpty(artifactId))
        {
            return new AnalyzeDumpSosResult(
                Success: false,
                Output: string.Empty,
                Error: "Artifact ID is required"
            );
        }

        if (string.IsNullOrEmpty(command))
        {
            return new AnalyzeDumpSosResult(
                Success: false,
                Output: string.Empty,
                Error: "SOS command is required"
            );
        }

        // Remove ! prefix if present (dotnet-dump analyze doesn't require it)
        if (command.StartsWith("!"))
        {
            command = command.Substring(1);
        }

        try
        {
            // Get artifact from store
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return new AnalyzeDumpSosResult(
                    Success: false,
                    Output: string.Empty,
                    Error: $"Artifact not found: {artifactId}"
                );
            }

            // Check if artifact is a dump
            if (artifact.Kind != ArtifactKind.Dump)
            {
                return new AnalyzeDumpSosResult(
                    Success: false,
                    Output: string.Empty,
                    Error: $"Artifact is not a dump file: {artifact.Kind}"
                );
            }

            // Validate artifact path is within artifact root (security: treat DB as untrusted)
            var artifactRoot = options.Value.GetArtifactRoot();
            PathSecurity.EnsurePathWithinDirectory(artifact.FilePath, artifactRoot);

            // Check if dotnet-sos is available
            if (!await sosAnalyzer.IsAvailableAsync(cancellationToken))
            {
                return new AnalyzeDumpSosResult(
                    Success: false,
                    Output: string.Empty,
                    Error: """
dotnet-dump CLI not found.

Install using one of the following methods:

1) Global:
   dotnet tool install --global dotnet-dump

2) Repo-local (recommended for teams):
   dotnet new tool-manifest   # if manifest doesn't exist yet
   dotnet tool install dotnet-dump
   dotnet tool restore

Restart MCP server / client after installation.
"""
                );
            }

            // Execute SOS command
            var result = await sosAnalyzer.ExecuteCommandAsync(artifact.FilePath, command, cancellationToken);

            return new AnalyzeDumpSosResult(
                Success: result.Success,
                Output: result.Output,
                Error: result.Error
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SOS analysis failed for artifact {ArtifactId}", artifactId);
            return new AnalyzeDumpSosResult(
                Success: false,
                Output: string.Empty,
                Error: $"SOS analysis failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "symbols_resolve")]
    public static async Task<SymbolsResolveResult> ResolveSymbols(
        string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var symbolResolver = server.Services!.GetRequiredService<ISymbolResolver>();
        var logger = server.Services!.GetRequiredService<ILogger<AnalysisTools>>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();

        if (string.IsNullOrEmpty(artifactId))
        {
            return new SymbolsResolveResult(
                Success: false,
                SymbolPath: string.Empty,
                Error: "Artifact ID is required"
            );
        }

        try
        {
            // Get artifact from store
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return new SymbolsResolveResult(
                    Success: false,
                    SymbolPath: string.Empty,
                    Error: $"Artifact not found: {artifactId}"
                );
            }

            // Check if artifact is a dump
            if (artifact.Kind != ArtifactKind.Dump)
            {
                return new SymbolsResolveResult(
                    Success: false,
                    SymbolPath: string.Empty,
                    Error: $"Symbol resolution is only supported for dump artifacts, got: {artifact.Kind}"
                );
            }

            // Validate artifact path is within artifact root (security: treat DB as untrusted)
            var artifactRoot = options.Value.GetArtifactRoot();
            PathSecurity.EnsurePathWithinDirectory(artifact.FilePath, artifactRoot);

            // Check if dotnet-symbol is available
            if (!await symbolResolver.IsAvailableAsync(cancellationToken))
            {
                return new SymbolsResolveResult(
                    Success: false,
                    SymbolPath: string.Empty,
                    Error: """
dotnet-symbol CLI not found.

Install using one of the following methods:

1) Global:
   dotnet tool install --global dotnet-symbol

2) Repo-local (recommended for teams):
   dotnet new tool-manifest   # if manifest doesn't exist yet
   dotnet tool install dotnet-symbol
   dotnet tool restore

Restart MCP server / client after installation.
"""
                );
            }

            // Resolve symbols using file path
            var result = await symbolResolver.ResolveAsync(artifact.FilePath, cancellationToken);

            return new SymbolsResolveResult(
                Success: result.Success,
                SymbolPath: result.SymbolPath,
                Error: result.Error
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Symbol resolution failed for artifact {ArtifactId}", artifactId);
            return new SymbolsResolveResult(
                Success: false,
                SymbolPath: string.Empty,
                Error: $"Symbol resolution failed: {ex.Message}"
            );
        }
    }
}

public record AnalyzeDumpSosResult(
    bool Success,
    string Output,
    string? Error
);

public record SymbolsResolveResult(
    bool Success,
    string SymbolPath,
    string? Error
);
