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
public sealed class ArtifactDeleteTools
{
    [McpServerTool(Name = "artifact_delete", Title = "Delete Artifact", Destructive = true, Idempotent = false), Description("Delete artifact by ID")]
    public static async Task<DeleteArtifactResult> DeleteArtifact(
        [Description("Artifact ID to delete")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return new DeleteArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: "Artifact ID must not be empty"
            );
        }

        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactDeleteTools>>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();

        try
        {
            var id = new ArtifactId(artifactId);
            var artifact = await artifactStore.GetAsync(id, cancellationToken);

            if (artifact == null)
            {
                logger.LogWarning("Artifact {ArtifactId} not found for deletion", artifactId);
                return new DeleteArtifactResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Message: "Artifact not found"
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

            if (File.Exists(artifact.FilePath))
            {
                File.Delete(artifact.FilePath);
            }
            
            await artifactStore.DeleteAsync(id, cancellationToken);
            
            logger.LogInformation("Deleted artifact {ArtifactId}", artifactId);
            
            return new DeleteArtifactResult(
                Success: true,
                ArtifactId: artifactId,
                Message: "Artifact deleted successfully"
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for artifact deletion: {Message}", ex.Message);
            return new DeleteArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Delete artifact failed for {ArtifactId}", artifactId);
            return new DeleteArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: $"Delete artifact failed: {ex.Message}"
            );
        }
    }
}
