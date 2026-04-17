using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Artifacts;

[McpServerToolType]
public sealed class ArtifactLifecycleTools
{
    [McpServerTool(Name = "artifact_cleanup", Title = "Cleanup Old Artifacts", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Delete artifacts based on strategy. Strategies: age (default, filter by age and pin status), duplicates (keep newest per PID+kind)")]
    public static async Task<CleanupArtifactsResult> CleanupArtifacts(
        [Description("Maximum age of artifacts to keep (e.g., 7d for 7 days)")] string maxAge,
        McpServer server,
        [Description("Maximum total size to delete in bytes (optional, e.g., 10737418240 for 10GB)")] long? maxSizeBytes = null,
        [Description("Cleanup strategy: 'age' (default, filter by age and pin status), 'duplicates' (keep newest per PID+kind)")] string strategy = "age",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(maxAge))
        {
            return new CleanupArtifactsResult(
                DeletedCount: 0,
                Message: "Max age must not be empty"
            );
        }

        if (!string.IsNullOrWhiteSpace(strategy))
        {
            var validStrategies = new[] { "age", "duplicates", "importance" };
            if (!validStrategies.Contains(strategy, StringComparer.OrdinalIgnoreCase))
            {
                return new CleanupArtifactsResult(
                    DeletedCount: 0,
                    Message: $"Strategy must be one of: {string.Join(", ", validStrategies)}"
                );
            }
        }

        if (maxSizeBytes.HasValue && maxSizeBytes.Value <= 0)
        {
            return new CleanupArtifactsResult(
                DeletedCount: 0,
                Message: "MaxSizeBytes must be greater than 0"
            );
        }

        var retentionService = server.Services!.GetRequiredService<IArtifactRetentionService>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactLifecycleTools>>();

        try
        {
            var timeSpan = TimeSpanParser.ParseMaxAge(maxAge);
            var deletedCount = await retentionService.CleanupOldArtifactsAsync(timeSpan, maxSizeBytes, strategy.ToLowerInvariant(), cancellationToken);

            var strategyText = strategy.ToLowerInvariant();
            var message = maxSizeBytes.HasValue
                ? $"Deleted {deletedCount} artifacts using '{strategyText}' strategy older than {maxAge} (max size: {maxSizeBytes} bytes)"
                : $"Deleted {deletedCount} artifacts using '{strategyText}' strategy older than {maxAge}";

            logger.LogInformation("Cleanup completed: {DeletedCount} artifacts deleted older than {MaxAge}, max size: {MaxSize}",
                deletedCount, maxAge, maxSizeBytes);

            return new CleanupArtifactsResult(
                DeletedCount: deletedCount,
                Message: message
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for artifact cleanup: {Message}", ex.Message);
            return new CleanupArtifactsResult(
                DeletedCount: 0,
                Message: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Artifact cleanup failed");
            return new CleanupArtifactsResult(
                DeletedCount: 0,
                Message: $"Cleanup failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "artifact_pin", Title = "Pin Artifact", ReadOnly = false, Idempotent = false, UseStructuredContent = true), Description("Pin artifact to protect from automatic deletion")]
    public static async Task<PinArtifactResult> PinArtifact(
        [Description("Artifact ID to pin")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return new PinArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: "Artifact ID must not be empty"
            );
        }

        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactLifecycleTools>>();

        try
        {
            var id = new ArtifactId(artifactId);
            var artifact = await artifactStore.GetAsync(id, cancellationToken);

            if (artifact == null)
            {
                logger.LogWarning("Artifact {ArtifactId} not found for pinning", artifactId);
                return new PinArtifactResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Message: "Artifact not found"
                );
            }

            var updatedArtifact = artifact with { Pinned = true };
            await artifactStore.UpdateAsync(updatedArtifact, cancellationToken);

            logger.LogInformation("Pinned artifact {ArtifactId}", artifactId);

            return new PinArtifactResult(
                Success: true,
                ArtifactId: artifactId,
                Message: "Artifact pinned successfully"
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for artifact pinning: {Message}", ex.Message);
            return new PinArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pin artifact failed for {ArtifactId}", artifactId);
            return new PinArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: $"Pin artifact failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "artifact_unpin", Title = "Unpin Artifact", ReadOnly = false, Idempotent = false, UseStructuredContent = true), Description("Unpin artifact to allow automatic deletion")]
    public static async Task<PinArtifactResult> UnpinArtifact(
        [Description("Artifact ID to unpin")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return new PinArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: "Artifact ID must not be empty"
            );
        }

        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactLifecycleTools>>();

        try
        {
            var id = new ArtifactId(artifactId);
            var artifact = await artifactStore.GetAsync(id, cancellationToken);

            if (artifact == null)
            {
                logger.LogWarning("Artifact {ArtifactId} not found for unpinning", artifactId);
                return new PinArtifactResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Message: "Artifact not found"
                );
            }

            var updatedArtifact = artifact with { Pinned = false };
            await artifactStore.UpdateAsync(updatedArtifact, cancellationToken);

            logger.LogInformation("Unpinned artifact {ArtifactId}", artifactId);

            return new PinArtifactResult(
                Success: true,
                ArtifactId: artifactId,
                Message: "Artifact unpinned successfully"
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for artifact unpinning: {Message}", ex.Message);
            return new PinArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unpin artifact failed for {ArtifactId}", artifactId);
            return new PinArtifactResult(
                Success: false,
                ArtifactId: artifactId,
                Message: $"Unpin artifact failed: {ex.Message}"
            );
        }
    }
}
