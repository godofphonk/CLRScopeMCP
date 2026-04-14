using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var logger = server.Services!.GetRequiredService<ILogger<AnalysisTools>>();
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var sosAnalyzer = server.Services!.GetRequiredService<ISosAnalyzer>();

        // Try to get artifact from store
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

        // Execute SOS command
        var result = await sosAnalyzer.ExecuteCommandAsync(artifact.FilePath, command, cancellationToken);

        return new AnalyzeDumpSosResult(
            Success: result.Success,
            Output: result.Output,
            Error: result.Error
        );
    }

    [McpServerTool(Name = "symbols_resolve")]
    public static async Task<SymbolsResolveResult> ResolveSymbols(
        string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var logger = server.Services!.GetRequiredService<ILogger<AnalysisTools>>();
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var symbolResolver = server.Services!.GetRequiredService<ISymbolResolver>();

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

        // Resolve symbols using file path
        var result = await symbolResolver.ResolveAsync(artifact.FilePath, cancellationToken);

        return new SymbolsResolveResult(
            Success: result.Success,
            SymbolPath: result.SymbolPath,
            Error: result.Error
        );
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
