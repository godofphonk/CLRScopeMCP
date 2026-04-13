using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ClrScope.Mcp.Tools.Resources;

[McpServerToolType]
public sealed class ResourceTools
{
    private static void ValidateArtifactPath(string filePath, string artifactRoot, ILogger logger)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var rootPath = Path.GetFullPath(artifactRoot);

            // Check if the full path starts with the artifact root
            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError("Path validation failed: {FilePath} is outside artifact root {ArtifactRoot}", fullPath, rootPath);
                throw new UnauthorizedAccessException($"File path is outside artifact root");
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            logger.LogError(ex, "Path validation failed for {FilePath}", filePath);
            throw new UnauthorizedAccessException("Invalid file path", ex);
        }
    }

    [McpServerTool(Name = "resource_artifact")]
    public static async Task<string> GetArtifactResource(
        string id,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ResourceTools>>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(id), cancellationToken);
            if (artifact == null)
            {
                return $"Error: Artifact not found: {id}";
            }

            var artifactRoot = options.Value.GetArtifactRoot();
            var preview = GetArtifactPreview(artifact, artifactRoot, logger);
            return preview;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get artifact resource: {ArtifactId}", id);
            return $"Error: Failed to get artifact: {ex.Message}";
        }
    }

    [McpServerTool(Name = "resource_artifact_metadata")]
    public static async Task<string> GetArtifactMetadataResource(
        string id,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ResourceTools>>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(id), cancellationToken);
            if (artifact == null)
            {
                return $"Error: Artifact not found: {id}";
            }

            var metadata = GetArtifactMetadata(artifact);
            return metadata;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get artifact metadata: {ArtifactId}", id);
            return $"Error: Failed to get artifact metadata: {ex.Message}";
        }
    }

    [McpServerTool(Name = "resource_artifact_summary")]
    public static async Task<string> GetArtifactSummaryResource(
        string id,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ResourceTools>>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(id), cancellationToken);
            if (artifact == null)
            {
                return $"Error: Artifact not found: {id}";
            }

            var summary = GetArtifactSummary(artifact);
            return summary;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get artifact summary: {ArtifactId}", id);
            return $"Error: Failed to get artifact summary: {ex.Message}";
        }
    }

    [McpServerTool(Name = "resource_session")]
    public static async Task<string> GetSessionResource(
        string id,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var sessionStore = server.Services!.GetRequiredService<ISqliteSessionStore>();
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ResourceTools>>();

        try
        {
            var sessionId = new SessionId(id);
            var session = await sessionStore.GetAsync(sessionId, cancellationToken);
            if (session == null)
            {
                return $"Error: Session not found: {id}";
            }

            var artifacts = await artifactStore.GetBySessionAsync(sessionId, cancellationToken);
            var resource = GetSessionResource(session, artifacts);
            return resource;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get session resource: {SessionId}", id);
            return $"Error: Failed to get session: {ex.Message}";
        }
    }

    private static string GetArtifactPreview(Artifact artifact, string artifactRoot, ILogger logger)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Artifact: {artifact.ArtifactId.Value}");
        sb.AppendLine();
        sb.AppendLine("## Metadata");
        sb.AppendLine($"- Kind: {artifact.Kind}");
        sb.AppendLine($"- Status: {artifact.Status}");
        sb.AppendLine($"- Size: {FormatBytes(artifact.SizeBytes)}");
        sb.AppendLine($"- PID: {artifact.Pid}");
        sb.AppendLine($"- Created: {artifact.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- File: {artifact.FilePath}");
        sb.AppendLine();
        sb.AppendLine("## Content Preview");

        try
        {
            ValidateArtifactPath(artifact.FilePath, artifactRoot, logger);

            if (System.IO.File.Exists(artifact.FilePath))
            {
                var content = System.IO.File.ReadAllText(artifact.FilePath);
                var preview = content.Length > 500 ? content.Substring(0, 500) + "..." : content;
                sb.AppendLine("```");
                sb.AppendLine(preview);
                sb.AppendLine("```");
            }
            else
            {
                sb.AppendLine("File not found.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Failed to read file: {ex.Message}");
        }
        
        return sb.ToString();
    }

    private static string GetArtifactMetadata(Artifact artifact)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Artifact Metadata: {artifact.ArtifactId.Value}");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(System.Text.Json.JsonSerializer.Serialize(artifact, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string GetArtifactSummary(Artifact artifact)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Artifact Summary: {artifact.ArtifactId.Value}");
        sb.AppendLine();
        sb.AppendLine("## Quick Facts");
        sb.AppendLine($"- Type: {artifact.Kind}");
        sb.AppendLine($"- Size: {FormatBytes(artifact.SizeBytes)}");
        sb.AppendLine($"- Status: {artifact.Status}");
        sb.AppendLine($"- Process ID: {artifact.Pid}");
        sb.AppendLine($"- Created: {artifact.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("## Location");
        sb.AppendLine($"- File: {artifact.FilePath}");
        sb.AppendLine();
        sb.AppendLine("## Analysis");
        sb.AppendLine("- This artifact was collected during a diagnostic session.");
        sb.AppendLine("- Use `analyze_dump_sos` or other analysis tools to examine the content.");
        return sb.ToString();
    }

    private static string GetSessionResource(Session session, IReadOnlyList<Artifact> artifacts)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Session: {session.SessionId.Value}");
        sb.AppendLine();
        sb.AppendLine("## Session Metadata");
        sb.AppendLine($"- Kind: {session.Kind}");
        sb.AppendLine($"- Status: {session.Status}");
        sb.AppendLine($"- PID: {session.Pid}");
        sb.AppendLine($"- Created: {session.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        if (session.CompletedAtUtc.HasValue)
        {
            sb.AppendLine($"- Completed: {session.CompletedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        }
        sb.AppendLine($"- Phase: {session.Phase}");
        sb.AppendLine($"- Artifact Count: {artifacts.Count}");
        sb.AppendLine();
        sb.AppendLine("## Artifacts");
        foreach (var artifact in artifacts)
        {
            sb.AppendLine($"- {artifact.ArtifactId.Value}: {artifact.Kind} ({FormatBytes(artifact.SizeBytes)})");
        }
        return sb.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
