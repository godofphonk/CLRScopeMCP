using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Artifacts;

[McpServerToolType]
public sealed class ArtifactReadTools
{
    [McpServerTool(Name = "artifact_read_text", Title = "Read Artifact Text", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read artifact content. Format: text (for Counters/Stacks), hex (hex dump for binary files), base64 (base64 encoded for binary files)")]
    public static async Task<ReadArtifactTextResult> ReadArtifactText(
        [Description("Artifact ID to read from")] string artifactId,
        McpServer server,
        [Description("Output format: 'text' (for Counters/Stacks), 'hex' (hex dump), 'base64' (base64 encoded)")] string format = "text",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return new ReadArtifactTextResult(
                Success: false,
                ArtifactId: artifactId,
                Content: null,
                Error: "Artifact ID must not be empty"
            );
        }

        if (format != "text" && format != "hex" && format != "base64")
        {
            return new ReadArtifactTextResult(
                Success: false,
                ArtifactId: artifactId,
                Content: null,
                Error: "Format must be 'text', 'hex', or 'base64'"
            );
        }

        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactReadTools>>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();

        try
        {
            var id = new ArtifactId(artifactId);
            var artifact = await artifactStore.GetAsync(id, cancellationToken);

            if (artifact == null)
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: null,
                    Error: "Artifact not found"
                );
            }

            if (format == "text" && artifact.Kind != ArtifactKind.Counters && artifact.Kind != ArtifactKind.Stacks)
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: null,
                    Error: $"Artifact kind '{artifact.Kind}' is not text-only. Use 'hex' or 'base64' format for binary files (Dump, Trace, GcDump)."
                );
            }

            if (format != "text" && format != "hex" && format != "base64")
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: null,
                    Error: $"Invalid format '{format}'. Supported formats: text, hex, base64."
                );
            }

            var artifactRoot = options.Value.GetArtifactRoot();
            try
            {
                PathSecurity.EnsurePathWithinDirectory(artifact.FilePath, artifactRoot);
            }
            catch (UnauthorizedAccessException)
            {
                logger.LogError("Path validation failed: {FilePath} is outside artifact root {ArtifactRoot}", artifact.FilePath, artifactRoot);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Path validation failed for {FilePath}", artifact.FilePath);
                throw new UnauthorizedAccessException("Invalid file path", ex);
            }

            if (!File.Exists(artifact.FilePath))
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: null,
                    Error: "File not found"
                );
            }

            var fileInfo = new FileInfo(artifact.FilePath);

            var maxSize = format switch
            {
                "text" => 1024 * 1024,
                "hex" => 1024 * 1024,
                "base64" => 10 * 1024 * 1024,
                _ => 1024 * 1024
            };

            if (fileInfo.Length > maxSize)
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: null,
                    Error: $"File too large for {format} reading (>{maxSize / 1024 / 1024}MB)"
                );
            }

            string content;
            switch (format.ToLowerInvariant())
            {
                case "text":
                    content = await File.ReadAllTextAsync(artifact.FilePath, cancellationToken);
                    logger.LogInformation("Read text content for artifact {ArtifactId} ({Length} bytes)", artifactId, content.Length);
                    break;
                case "hex":
                    var bytes = await File.ReadAllBytesAsync(artifact.FilePath, cancellationToken);
                    content = BitConverter.ToString(bytes).Replace("-", " ");
                    logger.LogInformation("Read hex content for artifact {ArtifactId} ({Length} bytes -> {HexLength} chars)", artifactId, bytes.Length, content.Length);
                    break;
                case "base64":
                    var base64Bytes = await File.ReadAllBytesAsync(artifact.FilePath, cancellationToken);
                    content = Convert.ToBase64String(base64Bytes);
                    logger.LogInformation("Read base64 content for artifact {ArtifactId} ({Length} bytes -> {Base64Length} chars)", artifactId, base64Bytes.Length, content.Length);
                    break;
                default:
                    content = string.Empty;
                    break;
            }

            return new ReadArtifactTextResult(
                Success: true,
                ArtifactId: artifactId,
                Content: content,
                Error: null
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for artifact read: {Message}", ex.Message);
            return new ReadArtifactTextResult(
                Success: false,
                ArtifactId: artifactId,
                Content: null,
                Error: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Read artifact text failed for {ArtifactId}", artifactId);
            return new ReadArtifactTextResult(
                Success: false,
                ArtifactId: artifactId,
                Content: null,
                Error: $"Read artifact text failed: {ex.Message}"
            );
        }
    }
}
