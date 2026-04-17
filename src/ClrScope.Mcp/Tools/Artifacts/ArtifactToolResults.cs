namespace ClrScope.Mcp.Tools.Artifacts;

public record ArtifactMetadataResult(
    bool Found,
    string ArtifactId,
    string? Kind,
    string? Status,
    long SizeBytes,
    string? Sha256,
    string? HashState,
    int Pid,
    DateTime? CreatedAtUtc,
    string? FilePath,
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
    long SizeBytes,
    DateTime CreatedAtUtc,
    string? FilePath = null
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

public record CleanupArtifactsResult(
    int DeletedCount,
    string Message
);
