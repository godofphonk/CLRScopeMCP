namespace ClrScope.Mcp.Domain.Sessions;

/// <summary>
/// Indicates how the trace collection completed
/// </summary>
public enum TraceCompletionMode
{
    /// <summary>
    /// Trace completed successfully with full rundown
    /// </summary>
    Complete,

    /// <summary>
    /// Trace was stopped forcibly (graceful stop timeout)
    /// May be missing rundown/symbols
    /// </summary>
    Partial,

    /// <summary>
    /// Collection was cancelled by user
    /// </summary>
    Cancelled,

    /// <summary>
    /// Collection failed due to error
    /// </summary>
    Failed,

    /// <summary>
    /// Target process completed before duration elapsed
    /// </summary>
    CompletedEarly
}
