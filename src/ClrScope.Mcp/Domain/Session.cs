namespace ClrScope.Mcp.Domain;

public enum SessionPhase
{
    Preflight,
    Attaching,
    Collecting,
    Persisting,
    Completed,
    Failed,
    Cancelled
}

public record Session(
    SessionId SessionId,
    SessionKind Kind,
    int Pid,
    SessionStatus Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? Error,
    string? Profile,
    SessionPhase Phase = SessionPhase.Preflight
);
