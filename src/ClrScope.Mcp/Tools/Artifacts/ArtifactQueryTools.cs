using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;

namespace ClrScope.Mcp.Tools.Artifacts;

[McpServerToolType]
public sealed class ArtifactQueryTools
{
    [McpServerTool(Name = "artifact_get_metadata", Title = "Get Artifact Metadata", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Get artifact metadata by ID")]
    public static async Task<ArtifactMetadataResult> GetArtifactMetadata(
        [Description("Artifact ID to get metadata for")] string artifactId,
        McpServer server,
        [Description("Include file path in response (default: false)")] bool includeFilePath = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return new ArtifactMetadataResult(
                Found: false,
                ArtifactId: artifactId,
                Kind: null,
                Status: null,
                SizeBytes: 0,
                Sha256: null,
                HashState: null,
                Pid: 0,
                CreatedAtUtc: null,
                FilePath: null,
                Error: "Artifact ID must not be empty"
            );
        }

        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactQueryTools>>();

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
                    HashState: null,
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
                SizeBytes: artifact.SizeBytes,
                Sha256: artifact.Sha256,
                HashState: artifact.HashState.ToString(),
                Pid: artifact.Pid,
                CreatedAtUtc: artifact.CreatedAtUtc,
                FilePath: includeFilePath ? artifact.FilePath : null,
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
                HashState: null,
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
                HashState: null,
                Pid: 0,
                CreatedAtUtc: null,
                Error: $"Get artifact metadata failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "artifact_list", Title = "List Artifacts", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("List artifacts with optional filtering by pid, kind, status, date range, session, and pagination")]
    public static async Task<ArtifactListResult> ListArtifacts(
        McpServer server,
        [Description("Filter by process ID (optional)")] int? pid = null,
        [Description("Filter by kind (optional)")] string? kind = null,
        [Description("Filter by status (optional)")] string? status = null,
        [Description("Filter by date from (ISO format, e.g., 2024-01-01)")] string? dateFrom = null,
        [Description("Filter by date to (ISO format, e.g., 2024-12-31)")] string? dateTo = null,
        [Description("Filter by session ID (optional)")] string? sessionId = null,
        [Description("Offset for pagination (default: 0)")] int offset = 0,
        [Description("Limit for pagination (default: 50, max: 500)")] int limit = 50,
        [Description("Include file path in response (default: false)")] bool includeFilePath = false,
        CancellationToken cancellationToken = default)
    {
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

        if (!string.IsNullOrWhiteSpace(dateFrom))
        {
            if (!DateTime.TryParseExact(dateFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var _))
            {
                return new ArtifactListResult(
                    Count: 0,
                    Total: 0,
                    Offset: offset,
                    Limit: limit,
                    HasMore: false,
                    Artifacts: Array.Empty<ArtifactSummary>(),
                    Error: "DateFrom must be in ISO format (YYYY-MM-DD)"
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(dateTo))
        {
            if (!DateTime.TryParseExact(dateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var _))
            {
                return new ArtifactListResult(
                    Count: 0,
                    Total: 0,
                    Offset: offset,
                    Limit: limit,
                    HasMore: false,
                    Artifacts: Array.Empty<ArtifactSummary>(),
                    Error: "DateTo must be in ISO format (YYYY-MM-DD)"
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(sessionId, @"^ses_[0-9a-f]{32}$"))
            {
                return new ArtifactListResult(
                    Count: 0,
                    Total: 0,
                    Offset: offset,
                    Limit: limit,
                    HasMore: false,
                    Artifacts: Array.Empty<ArtifactSummary>(),
                    Error: "SessionId must be in the format 'ses_' followed by 32 lowercase hex characters (e.g. ses_a1b2c3d4e5f6...)"
                );
            }
        }

        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<ArtifactQueryTools>>();

        try
        {
            var artifacts = await artifactStore.GetAllAsync(cancellationToken);

            var filtered = artifacts.AsEnumerable();

            if (!string.IsNullOrEmpty(sessionId))
            {
                filtered = filtered.Where(a => a.SessionId.Value == sessionId);
            }

            if (pid.HasValue && pid.Value > 0)
            {
                filtered = filtered.Where(a => a.Pid == pid.Value);
            }

            if (!string.IsNullOrEmpty(kind) && Enum.TryParse<ArtifactKind>(kind, true, out var kindFilter))
            {
                filtered = filtered.Where(a => a.Kind == kindFilter);
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ArtifactStatus>(status, true, out var statusFilter))
            {
                filtered = filtered.Where(a => a.Status == statusFilter);
            }

            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
            {
                filtered = filtered.Where(a => a.CreatedAtUtc >= fromDate);
            }

            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var toDate))
            {
                filtered = filtered.Where(a => a.CreatedAtUtc <= toDate.AddDays(1).AddTicks(-1));
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
                    a.SizeBytes,
                    a.CreatedAtUtc,
                    includeFilePath ? a.FilePath : null
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
}
