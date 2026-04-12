using ClrScope.Mcp.Domain;

namespace ClrScope.Mcp.Infrastructure;

public interface ISqliteArtifactStore
{
    Task<Artifact?> GetAsync(ArtifactId artifactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Artifact>> GetBySessionAsync(SessionId sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Artifact>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Artifact> CreateAsync(
        ArtifactKind kind,
        string filePath,
        long sizeBytes,
        int pid,
        SessionId sessionId,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Artifact artifact, CancellationToken cancellationToken = default);
    Task DeleteAsync(ArtifactId artifactId, CancellationToken cancellationToken = default);
}
