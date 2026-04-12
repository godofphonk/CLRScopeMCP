namespace ClrScope.Mcp.Domain;

public record Artifact(
    ArtifactId ArtifactId,
    ArtifactKind Kind,
    ArtifactStatus Status,
    string FilePath,
    string? DiagUri,
    string? FileUri,
    string Sha256,
    long SizeBytes,
    int Pid,
    SessionId SessionId,
    DateTime CreatedAtUtc
);
