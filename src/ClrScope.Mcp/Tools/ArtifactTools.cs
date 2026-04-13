using ClrScope.Mcp.Domain;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

[McpServerToolType]
public sealed class ArtifactTools
{
    [McpServerTool(Name = "artifact_get_metadata", Title = "Get Artifact Metadata", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Получение метаданных артефакта по ID")]
    public static async Task<ArtifactMetadataResult> GetArtifactMetadata(
        [Description("Artifact ID to get metadata for")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                throw new ArgumentException("Artifact ID must not be empty", nameof(artifactId));
            }

            var id = new ArtifactId(artifactId);
            var artifact = await artifactStore.GetAsync(id, cancellationToken);
            
            if (artifact == null)
            {
                logger.LogWarning("Artifact {ArtifactId} not found", artifactId);
                return new ArtifactMetadataResult(
                    Found: false,
                    ArtifactId: artifactId,
                    Kind: string.Empty,
                    Status: string.Empty,
                    FilePath: string.Empty,
                    SizeBytes: 0,
                    Sha256: string.Empty,
                    Pid: 0,
                    CreatedAtUtc: DateTime.UtcNow,
                    Error: "Artifact not found"
                );
            }

            logger.LogInformation("Retrieved metadata for artifact {ArtifactId}", artifactId);

            return new ArtifactMetadataResult(
                Found: true,
                ArtifactId: artifact.ArtifactId.Value,
                Kind: artifact.Kind.ToString(),
                Status: artifact.Status.ToString(),
                FilePath: artifact.FilePath,
                SizeBytes: artifact.SizeBytes,
                Sha256: artifact.Sha256,
                Pid: artifact.Pid,
                CreatedAtUtc: artifact.CreatedAtUtc,
                Error: string.Empty
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for artifact metadata: {Message}", ex.Message);
            return new ArtifactMetadataResult(
                Found: false,
                ArtifactId: artifactId,
                Kind: string.Empty,
                Status: string.Empty,
                FilePath: string.Empty,
                SizeBytes: 0,
                Sha256: string.Empty,
                Pid: 0,
                CreatedAtUtc: DateTime.UtcNow,
                Error: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Get artifact metadata failed for {ArtifactId}", artifactId);
            return new ArtifactMetadataResult(
                Found: false,
                ArtifactId: artifactId,
                Kind: string.Empty,
                Status: string.Empty,
                FilePath: string.Empty,
                SizeBytes: 0,
                Sha256: string.Empty,
                Pid: 0,
                CreatedAtUtc: DateTime.UtcNow,
                Error: $"Get artifact metadata failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "artifact_list", Title = "List Artifacts", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Список всех артефактов с опциональной фильтрацией")]
    public static async Task<ArtifactListResult> ListArtifacts(
        McpServer server,
        [Description("Filter by kind (optional)")] string? kind = null,
        [Description("Filter by status (optional)")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            var artifacts = await artifactStore.GetAllAsync(cancellationToken);
            
            var filtered = artifacts.AsEnumerable();
            
            if (!string.IsNullOrEmpty(kind) && Enum.TryParse<ArtifactKind>(kind, true, out var kindFilter))
            {
                filtered = filtered.Where(a => a.Kind == kindFilter);
            }
            
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ArtifactStatus>(status, true, out var statusFilter))
            {
                filtered = filtered.Where(a => a.Status == statusFilter);
            }
            
            var result = filtered.ToArray();
            
            logger.LogInformation("Listed {Count} artifacts", result.Length);
            
            return new ArtifactListResult(
                Count: result.Length,
                Artifacts: result.Select(a => new ArtifactSummary(
                    a.ArtifactId.Value,
                    a.Kind.ToString(),
                    a.Status.ToString(),
                    a.FilePath,
                    a.SizeBytes,
                    a.CreatedAtUtc
                )).ToArray()
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "List artifacts failed");
            return new ArtifactListResult(
                Count: 0,
                Artifacts: Array.Empty<ArtifactSummary>()
            );
        }
    }

    [McpServerTool(Name = "artifact_delete", Title = "Delete Artifact", Destructive = true, Idempotent = false), Description("Удаление артефакта по ID")]
    public static async Task<DeleteArtifactResult> DeleteArtifact(
        [Description("Artifact ID to delete")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                throw new ArgumentException("Artifact ID must not be empty", nameof(artifactId));
            }

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

    [McpServerTool(Name = "artifact_read_text", Title = "Read Artifact Text", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Чтение текстового содержимого артефакта (если применимо)")]
    public static async Task<ReadArtifactTextResult> ReadArtifactText(
        [Description("Artifact ID to read text from")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                throw new ArgumentException("Artifact ID must not be empty", nameof(artifactId));
            }

            var id = new ArtifactId(artifactId);
            var artifact = await artifactStore.GetAsync(id, cancellationToken);
            
            if (artifact == null)
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: string.Empty,
                    Error: "Artifact not found"
                );
            }

            if (!File.Exists(artifact.FilePath))
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: string.Empty,
                    Error: "File not found"
                );
            }

            var fileInfo = new FileInfo(artifact.FilePath);
            if (fileInfo.Length > 1024 * 1024)
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: string.Empty,
                    Error: "File too large for text reading (>1MB)"
                );
            }

            var content = await File.ReadAllTextAsync(artifact.FilePath, cancellationToken);

            logger.LogInformation("Read text content for artifact {ArtifactId} ({Length} bytes)", artifactId, content.Length);

            return new ReadArtifactTextResult(
                Success: true,
                ArtifactId: artifactId,
                Content: content,
                Error: string.Empty
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for artifact read: {Message}", ex.Message);
            return new ReadArtifactTextResult(
                Success: false,
                ArtifactId: artifactId,
                Content: string.Empty,
                Error: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Read artifact text failed for {ArtifactId}", artifactId);
            return new ReadArtifactTextResult(
                Success: false,
                ArtifactId: artifactId,
                Content: string.Empty,
                Error: $"Read artifact text failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "artifact_cleanup", Title = "Cleanup Old Artifacts", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true), Description("Удаление старых артефактов старше указанного возраста и/или ограничение общего размера")]
    public static async Task<CleanupArtifactsResult> CleanupArtifacts(
        [Description("Максимальный возраст артефактов для сохранения (например, 7d для 7 дней)")] string maxAge,
        McpServer server,
        [Description("Максимальный общий размер для удаления в байтах (опционально, например, 10737418240 для 10GB)")] long? maxSizeBytes = null,
        CancellationToken cancellationToken = default)
    {
        var retentionService = server.Services!.GetRequiredService<IArtifactRetentionService>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(maxAge))
            {
                throw new ArgumentException("Max age must not be empty", nameof(maxAge));
            }

            var timeSpan = ParseMaxAge(maxAge);
            var deletedCount = await retentionService.CleanupOldArtifactsAsync(timeSpan, maxSizeBytes, cancellationToken);

            var message = maxSizeBytes.HasValue
                ? $"Deleted {deletedCount} artifacts older than {maxAge} (max size: {maxSizeBytes} bytes)"
                : $"Deleted {deletedCount} artifacts older than {maxAge}";

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

    private static TimeSpan ParseMaxAge(string maxAge)
    {
        // Parse format like "7d", "24h", "60m", "3600s"
        var value = int.Parse(maxAge.TrimEnd('d', 'h', 'm', 's'));
        var unit = maxAge[^1];

        return unit switch
        {
            'd' => TimeSpan.FromDays(value),
            'h' => TimeSpan.FromHours(value),
            'm' => TimeSpan.FromMinutes(value),
            's' => TimeSpan.FromSeconds(value),
            _ => throw new FormatException($"Unknown time unit: {unit}. Use d, h, m, or s.")
        };
    }

    [McpServerTool(Name = "artifact_pin", Title = "Pin Artifact", ReadOnly = false, Idempotent = false, UseStructuredContent = true), Description("Закрепить артефакт для защиты от автоматического удаления")]
    public static async Task<PinArtifactResult> PinArtifact(
        [Description("Artifact ID to pin")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                throw new ArgumentException("Artifact ID must not be empty", nameof(artifactId));
            }

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

    [McpServerTool(Name = "artifact_unpin", Title = "Unpin Artifact", ReadOnly = false, Idempotent = false, UseStructuredContent = true), Description("Открепить артефакт для разрешения автоматического удаления")]
    public static async Task<PinArtifactResult> UnpinArtifact(
        [Description("Artifact ID to unpin")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                throw new ArgumentException("Artifact ID must not be empty", nameof(artifactId));
            }

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

public record ArtifactMetadataResult(
    bool Found,
    string ArtifactId,
    string? Kind,
    string? Status,
    string? FilePath,
    long SizeBytes,
    string? Sha256,
    int Pid,
    DateTime? CreatedAtUtc,
    string? Error
);

public record ArtifactListResult(
    int Count,
    ArtifactSummary[] Artifacts
);

public record ArtifactSummary(
    string ArtifactId,
    string Kind,
    string Status,
    string FilePath,
    long SizeBytes,
    DateTime CreatedAtUtc
);

public record DeleteArtifactResult(
    bool Success,
    string ArtifactId,
    string Message
);

public record ReadArtifactTextResult(
    bool Success,
    string ArtifactId,
    string? Content,
    string? Error
);

public record PinArtifactResult(
    bool Success,
    string ArtifactId,
    string Message
);
