using ClrScope.Mcp.Domain.Sessions;

namespace ClrScope.Mcp.Infrastructure;

public interface ISqliteSessionStore
{
    Task<Session?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default);
    Task<Session> CreateAsync(SessionKind kind, int pid, string? profile = null, CancellationToken cancellationToken = default);
    Task<Session> CreateAsync(SessionKind kind, int pid, string? profile, string? incidentId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Session session, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken = default);
}
