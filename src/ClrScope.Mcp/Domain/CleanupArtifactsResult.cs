namespace ClrScope.Mcp.Domain;

/// <summary>
/// Result of artifact cleanup operation
/// </summary>
public record CleanupArtifactsResult(
    int DeletedCount,
    string Message
);
