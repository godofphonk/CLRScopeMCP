namespace ClrScope.Mcp.Domain.Sessions;

/// <summary>
/// Phases of a collection operation
/// </summary>
public enum CollectionPhase
{
    Preflight,
    Attaching,
    Collecting,
    Persisting,
    Completed,
    Failed,
    Cancelled
}
