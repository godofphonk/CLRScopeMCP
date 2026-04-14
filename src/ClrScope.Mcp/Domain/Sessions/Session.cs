namespace ClrScope.Mcp.Domain.Sessions;

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
    SessionPhase Phase = SessionPhase.Preflight,
    string? IncidentId = null)
{
    /// <summary>
    /// Transitions session to Failed state with CompletedAtUtc set.
    /// Enforces invariant: terminal states must have CompletedAtUtc.
    /// Preserves existing CompletedAtUtc if already set (e.g., by session_cancel).
    /// </summary>
    public Session AsFailed(string? error = null)
    {
        return this with
        {
            Status = SessionStatus.Failed,
            Phase = SessionPhase.Failed,
            Error = error,
            CompletedAtUtc = CompletedAtUtc ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Transitions session to Cancelled state with CompletedAtUtc set.
    /// Enforces invariant: terminal states must have CompletedAtUtc.
    /// Preserves existing CompletedAtUtc if already set (e.g., by session_cancel).
    /// </summary>
    public Session AsCancelled()
    {
        return this with
        {
            Status = SessionStatus.Cancelled,
            Phase = SessionPhase.Cancelled,
            CompletedAtUtc = CompletedAtUtc ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Transitions session to Completed state with CompletedAtUtc set.
    /// Enforces invariant: terminal states must have CompletedAtUtc.
    /// Preserves existing CompletedAtUtc if already set (e.g., by session_cancel).
    /// </summary>
    public Session AsCompleted()
    {
        return this with
        {
            Status = SessionStatus.Completed,
            Phase = SessionPhase.Completed,
            CompletedAtUtc = CompletedAtUtc ?? DateTime.UtcNow
        };
    }
}
