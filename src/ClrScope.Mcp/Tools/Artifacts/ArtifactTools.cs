using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
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
public sealed class ArtifactTools
{
    private static void ValidateArtifactPath(string filePath, string artifactRoot, ILogger logger)
    {
        try
        {
            PathSecurity.EnsurePathWithinDirectory(filePath, artifactRoot);
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogError("Path validation failed: {FilePath} is outside artifact root {ArtifactRoot}", filePath, artifactRoot);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Path validation failed for {FilePath}", filePath);
            throw new UnauthorizedAccessException("Invalid file path", ex);
        }
    }

    [McpServerTool(Name = "artifact_get_metadata", Title = "Get Artifact Metadata", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Get artifact metadata by ID")]
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
                    Error: "Artifact ID must not be empty"
                );
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

    [McpServerTool(Name = "artifact_list", Title = "List Artifacts", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("List artifacts with optional filtering by pid, kind, status, date range, and pagination")]
    public static async Task<ArtifactListResult> ListArtifacts(
        McpServer server,
        [Description("Filter by process ID (optional)")] int? pid = null,
        [Description("Filter by kind (optional)")] string? kind = null,
        [Description("Filter by status (optional)")] string? status = null,
        [Description("Filter by date from (ISO format, e.g., 2024-01-01)")] string? dateFrom = null,
        [Description("Filter by date to (ISO format, e.g., 2024-12-31)")] string? dateTo = null,
        [Description("Offset for pagination (default: 0)")] int offset = 0,
        [Description("Limit for pagination (default: 50, max: 500)")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            // Validate pagination parameters
            if (offset < 0)
            {
                return new ArtifactListResult(
                    Count: 0,
                    Total: 0,
                    Offset: 0,
                    Limit: limit,
                    HasMore: false,
                    Artifacts: Array.Empty<ArtifactSummary>(),
                    Error: "Offset must be >= 0"
                );
            }

            if (limit < 1 || limit > 500)
            {
                return new ArtifactListResult(
                    Count: 0,
                    Total: 0,
                    Offset: offset,
                    Limit: limit,
                    HasMore: false,
                    Artifacts: Array.Empty<ArtifactSummary>(),
                    Error: "Limit must be between 1 and 500"
                );
            }

            var artifacts = await artifactStore.GetAllAsync(cancellationToken);

            var filtered = artifacts.AsEnumerable();

            // Filter by PID
            if (pid.HasValue && pid.Value > 0)
            {
                filtered = filtered.Where(a => a.Pid == pid.Value);
            }

            // Filter by kind
            if (!string.IsNullOrEmpty(kind) && Enum.TryParse<ArtifactKind>(kind, true, out var kindFilter))
            {
                filtered = filtered.Where(a => a.Kind == kindFilter);
            }

            // Filter by status
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ArtifactStatus>(status, true, out var statusFilter))
            {
                filtered = filtered.Where(a => a.Status == statusFilter);
            }

            // Filter by date range
            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
            {
                filtered = filtered.Where(a => a.CreatedAtUtc >= fromDate);
            }

            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var toDate))
            {
                filtered = filtered.Where(a => a.CreatedAtUtc <= toDate.AddDays(1).AddTicks(-1)); // End of the day
            }

            var totalCount = filtered.Count();
            var paginated = filtered.Skip(offset).Take(limit).ToArray();

            logger.LogInformation("Listed {Count} artifacts (offset={Offset}, limit={Limit}, total={Total})", paginated.Length, offset, limit, totalCount);

            return new ArtifactListResult(
                Count: paginated.Length,
                Total: totalCount,
                Offset: offset,
                Limit: limit,
                HasMore: offset + limit < totalCount,
                Artifacts: paginated.Select(a => new ArtifactSummary(
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
                Total: 0,
                Offset: 0,
                Limit: limit,
                HasMore: false,
                Artifacts: Array.Empty<ArtifactSummary>(),
                Error: $"List artifacts failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "artifact_delete", Title = "Delete Artifact", Destructive = true, Idempotent = false), Description("Delete artifact by ID")]
    public static async Task<DeleteArtifactResult> DeleteArtifact(
        [Description("Artifact ID to delete")] string artifactId,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();

        try
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                return new DeleteArtifactResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Message: "Artifact ID must not be empty"
                );
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

            var artifactRoot = options.Value.GetArtifactRoot();
            ValidateArtifactPath(artifact.FilePath, artifactRoot, logger);

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

    [McpServerTool(Name = "artifact_read_text", Title = "Read Artifact Text", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read artifact content. Format: text (for Counters/Stacks), hex (hex dump for binary files), base64 (base64 encoded for binary files)")]
    public static async Task<ReadArtifactTextResult> ReadArtifactText(
        [Description("Artifact ID to read from")] string artifactId,
        McpServer server,
        [Description("Output format: 'text' (for Counters/Stacks), 'hex' (hex dump), 'base64' (base64 encoded)")] string format = "text",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();

        try
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: string.Empty,
                    Error: "Artifact ID must not be empty"
                );
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

            // Validate format for text-only artifact kinds
            if (format == "text" && artifact.Kind != ArtifactKind.Counters && artifact.Kind != ArtifactKind.Stacks)
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: string.Empty,
                    Error: $"Artifact kind '{artifact.Kind}' is not text-only. Use 'hex' or 'base64' format for binary files (Dump, Trace, GcDump)."
                );
            }

            // Validate format parameter
            if (format != "text" && format != "hex" && format != "base64")
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: string.Empty,
                    Error: $"Invalid format '{format}'. Supported formats: text, hex, base64."
                );
            }

            var artifactRoot = options.Value.GetArtifactRoot();
            ValidateArtifactPath(artifact.FilePath, artifactRoot, logger);

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

            // Adjust size limit based on format
            var maxSize = format switch
            {
                "text" => 1024 * 1024, // 1MB for text
                "hex" => 1024 * 1024, // 1MB for hex (will be ~2x output)
                "base64" => 10 * 1024 * 1024, // 10MB for base64 (will be ~1.33x output)
                _ => 1024 * 1024
            };

            if (fileInfo.Length > maxSize)
            {
                return new ReadArtifactTextResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Content: string.Empty,
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

    [McpServerTool(Name = "artifact_cleanup", Title = "Cleanup Old Artifacts", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Delete artifacts based on strategy. Strategies: age (default), importance (keep pinned), duplicates (keep newest per PID+kind)")]
    public static async Task<CleanupArtifactsResult> CleanupArtifacts(
        [Description("Maximum age of artifacts to keep (e.g., 7d for 7 days)")] string maxAge,
        McpServer server,
        [Description("Maximum total size to delete in bytes (optional, e.g., 10737418240 for 10GB)")] long? maxSizeBytes = null,
        [Description("Cleanup strategy: 'age' (default), 'importance' (keep pinned), 'duplicates' (keep newest per PID+kind)")] string strategy = "age",
        CancellationToken cancellationToken = default)
    {
        var retentionService = server.Services!.GetRequiredService<IArtifactRetentionService>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(maxAge))
            {
                return new CleanupArtifactsResult(
                    DeletedCount: 0,
                    Message: "Max age must not be empty"
                );
            }

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
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                return new PinArtifactResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Message: "Artifact ID must not be empty"
                );
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

    [McpServerTool(Name = "artifact_unpin", Title = "Unpin Artifact", ReadOnly = false, Idempotent = false, UseStructuredContent = true), Description("Unpin artifact to allow automatic deletion")]
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
                return new PinArtifactResult(
                    Success: false,
                    ArtifactId: artifactId,
                    Message: "Artifact ID must not be empty"
                );
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
    int Total,
    int Offset,
    int Limit,
    bool HasMore,
    ArtifactSummary[] Artifacts,
    string? Error = null
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
