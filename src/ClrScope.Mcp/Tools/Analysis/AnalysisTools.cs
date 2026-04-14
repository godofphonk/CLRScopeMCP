using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Analysis;

[McpServerToolType]
public sealed class AnalysisTools
{
    [McpServerTool(Name = "analyze_dump_sos"), Description("SOS analysis of dump file with custom commands. Use 'command' for single command or 'commands' for sequence")]
    public static async Task<AnalyzeDumpSosResult> AnalyzeDumpSos(
        string artifactId,
        McpServer server,
        [Description("Single SOS command (e.g., 'threads', 'clrstack', 'dumpheap -stat')")] string? command = null,
        [Description("Sequence of SOS commands to execute in order")] string[]? commands = null,
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

        // Determine which commands to execute
        string[] commandsToExecute;
        if (commands != null && commands.Length > 0)
        {
            commandsToExecute = commands;
        }
        else if (!string.IsNullOrEmpty(command))
        {
            commandsToExecute = new[] { command };
        }
        else
        {
            return new AnalyzeDumpSosResult(
                Success: false,
                Output: string.Empty,
                Error: "Either 'command' or 'commands' parameter is required"
            );
        }

        // Remove ! prefix from all commands if present
        commandsToExecute = commandsToExecute.Select(c => c.StartsWith("!") ? c.Substring(1) : c).ToArray();

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

            // Execute SOS commands sequentially
            var outputs = new List<string>();
            var errors = new List<string>();
            bool allSuccess = true;

            for (int i = 0; i < commandsToExecute.Length; i++)
            {
                var cmd = commandsToExecute[i];
                logger.LogInformation("Executing SOS command {CommandIndex}/{TotalCommands}: {Command}", i + 1, commandsToExecute.Length, cmd);

                var result = await sosAnalyzer.ExecuteCommandAsync(artifact.FilePath, cmd, cancellationToken);

                if (result.Success)
                {
                    outputs.Add($"=== Command {i + 1}/{commandsToExecute.Length}: {cmd} ===");
                    outputs.Add(result.Output);
                    outputs.Add(string.Empty); // Empty line between commands
                }
                else
                {
                    allSuccess = false;
                    errors.Add($"Command {i + 1}/{commandsToExecute.Length} ({cmd}) failed: {result.Error}");
                    outputs.Add($"=== Command {i + 1}/{commandsToExecute.Length}: {cmd} ===");
                    outputs.Add($"Error: {result.Error}");
                    outputs.Add(string.Empty);
                }
            }

            var combinedOutput = string.Join("\n", outputs);
            var combinedError = errors.Count > 0 ? string.Join("\n", errors) : null;

            return new AnalyzeDumpSosResult(
                Success: allSuccess,
                Output: combinedOutput,
                Error: combinedError
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

    [McpServerTool(Name = "symbols_resolve"), Description("Load symbols for artifact via dotnet-symbol")]
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
