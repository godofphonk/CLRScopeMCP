namespace ClrScope.Mcp.Domain;

public record Session(
    SessionId SessionId,
    SessionKind Kind,
    int Pid,
    SessionStatus Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? Error,
    string? Profile
);
