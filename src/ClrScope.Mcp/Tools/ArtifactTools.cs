using ClrScope.Mcp.Domain;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

[McpServerToolType]
public sealed class ArtifactTools
{
    [McpServerTool(Name = "artifact.get_metadata", Title = "Get Artifact Metadata", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Получение метаданных артефакта по ID")]
    public static async Task<ArtifactMetadataResult> GetArtifactMetadata(
        [Description("Artifact ID to get metadata for")] string artifactId,
        ISqliteArtifactStore artifactStore,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            throw new ArgumentException("Artifact ID must not be empty", nameof(artifactId));
        }

        try
        {
            var id = new ArtifactId(artifactId);
            var artifact = await artifactStore.GetAsync(id, cancellationToken);
            
            if (artifact == null)
            {
                logger.LogWarning("Artifact {ArtifactId} not found", artifactId);
                return new ArtifactMetadataResult(
                    Found: false,
                    ArtifactId: artifactId,
                    Kind: null,
                    Status: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Pid: 0,
                    CreatedAtUtc: null,
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
                Error: null
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for artifact metadata: {Message}", ex.Message);
            return new ArtifactMetadataResult(
                Found: false,
                ArtifactId: artifactId,
                Kind: null,
                Status: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Pid: 0,
                CreatedAtUtc: null,
                Error: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Get artifact metadata failed for {ArtifactId}", artifactId);
            return new ArtifactMetadataResult(
                Found: false,
                ArtifactId: artifactId,
                Kind: null,
                Status: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Pid: 0,
                CreatedAtUtc: null,
                Error: $"Get artifact metadata failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "artifact.list", Title = "List Artifacts", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Список всех артефактов с опциональной фильтрацией")]
    public static async Task<ArtifactListResult> ListArtifacts(
        ISqliteArtifactStore artifactStore,
        ILogger logger,
        [Description("Filter by kind (optional)")] string? kind = null,
        [Description("Filter by status (optional)")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var artifacts = await artifactStore.GetAllAsync(cancellationToken);
            
            // Apply filters
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

    [McpServerTool(Name = "artifact.delete", Title = "Delete Artifact", Destructive = true, Idempotent = false), Description("Удаление артефакта по ID")]
    public static async Task<DeleteArtifactResult> DeleteArtifact(
        [Description("Artifact ID to delete")] string artifactId,
        ISqliteArtifactStore artifactStore,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            throw new ArgumentException("Artifact ID must not be empty", nameof(artifactId));
        }

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
            
            // Delete file
            if (File.Exists(artifact.FilePath))
            {
                File.Delete(artifact.FilePath);
            }
            
            // Delete from database
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

    [McpServerTool(Name = "artifact.read_text", Title = "Read Artifact Text", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Чтение текстового содержимого артефакта (если применимо)")]
    public static async Task<ReadArtifactTextResult> ReadArtifactText(
        [Description("Artifact ID to read text from")] string artifactId,
        ISqliteArtifactStore artifactStore,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            throw new ArgumentException("Artifact ID must not be empty", nameof(artifactId));
        }

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
            
            if (!File.Exists(artifact.FilePath))
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: null,
                    Error: "File not found"
                );
            }
            
            // Check if file is likely text (limit size)
            var fileInfo = new FileInfo(artifact.FilePath);
            if (fileInfo.Length > 1024 * 1024) // 1MB limit
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: null,
                    Error: "File too large for text reading (>1MB)"
                );
            }
            
            var content = await File.ReadAllTextAsync(artifact.FilePath, cancellationToken);
            
            logger.LogInformation("Read text content for artifact {ArtifactId} ({Length} bytes)", artifactId, content.Length);
            
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
