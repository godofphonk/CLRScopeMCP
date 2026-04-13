namespace ClrScope.Mcp.Domain.Artifacts;

/// <summary>
/// Result of artifact cleanup operation
/// </summary>
public record CleanupArtifactsResult(
    int DeletedCount,
    string Message
);
