namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Registry for tracking active operations and supporting cancellation by session ID
/// </summary>
public interface IActiveOperationRegistry
{
    /// <summary>
    /// Register an active operation with its cancellation token source
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="cts">Cancellation token source for the operation</param>
    /// <returns>True if registration succeeded, false if session ID already registered</returns>
    bool TryRegister(Domain.SessionId sessionId, CancellationTokenSource cts);

    /// <summary>
    /// Cancel an active operation by session ID
    /// </summary>
    /// <param name="sessionId">Session ID to cancel</param>
    /// <param name="reason">Reason for cancellation</param>
    /// <returns>True if cancellation succeeded, false if session not found or already completed</returns>
    bool TryCancel(Domain.SessionId sessionId, string reason);

    /// <summary>
    /// Mark an operation as completed and remove from registry
    /// </summary>
    /// <param name="sessionId">Session ID to complete</param>
    void Complete(Domain.SessionId sessionId);
}
