namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Interface for serializing operations on a single PID
/// </summary>
public interface IPidLockManager
{
    /// <summary>
    /// Acquire a lock for operations on the specified PID
    /// </summary>
    /// <param name="pid">Process ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Lock handle that should be disposed when done</returns>
    Task<IDisposable> AcquireLockAsync(int pid, CancellationToken cancellationToken = default);
}
