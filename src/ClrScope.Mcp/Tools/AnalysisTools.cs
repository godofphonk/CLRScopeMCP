using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

public sealed class AnalysisTools
{
    [McpServerTool(Name = "analyze_dump_sos", Title = "Analyze Dump with SOS", ReadOnly = false, Idempotent = false), Description("SOS analysis of dump file with custom commands (Stage 2)")]
    public static async Task<AnalyzeDumpSosResult> AnalyzeDumpSos(
        [Description("Artifact ID of the dump file to analyze")] string artifactId,
        [Description("SOS command to execute (e.g., '!dumpheap -stat', '!threads', '!clrstack')")] string command,
        ISqliteArtifactStore artifactStore,
        ISosAnalyzer sosAnalyzer,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(artifactId))
        {
            throw new ArgumentException("Artifact ID is required", nameof(artifactId));
        }

        if (string.IsNullOrEmpty(command))
        {
            throw new ArgumentException("SOS command is required", nameof(command));
        }

        try
        {
            // Parse artifact ID
            if (!Guid.TryParse(artifactId, out var artifactGuid))
            {
                return new AnalyzeDumpSosResult(
                    Success: false,
                    Output: string.Empty,
                    Error: "Invalid artifact ID format"
                );
            }

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

    [McpServerTool(Name = "symbols_resolve", Title = "Resolve Symbols", ReadOnly = false, Idempotent = false), Description("Load symbols for artifact via dotnet-symbol (Stage 2)")]
    public static async Task<SymbolsResolveResult> ResolveSymbols(
        [Description("Artifact ID to resolve symbols for")] string artifactId,
        ISymbolResolver symbolResolver,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(artifactId))
        {
            throw new ArgumentException("Artifact ID is required", nameof(artifactId));
        }

        try
        {
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

            // Resolve symbols
            var result = await symbolResolver.ResolveAsync(artifactId, cancellationToken);

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
