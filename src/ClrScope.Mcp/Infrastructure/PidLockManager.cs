namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Serializes operations on a single PID using SemaphoreSlim
/// </summary>
public class PidLockManager : IPidLockManager, IDisposable
{
    private readonly Dictionary<int, SemaphoreSlim> _locks = new();
    private readonly object _syncRoot = new();

    public async Task<IDisposable> AcquireLockAsync(int pid, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim semaphore;

        lock (_syncRoot)
        {
            if (!_locks.TryGetValue(pid, out semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _locks[pid] = semaphore;
            }
        }

        await semaphore.WaitAsync(cancellationToken);
        return new PidLockHandle(() => ReleaseLock(pid, semaphore));
    }

    private void ReleaseLock(int pid, SemaphoreSlim semaphore)
    {
        semaphore.Release();
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            foreach (var semaphore in _locks.Values)
            {
                semaphore.Dispose();
            }
            _locks.Clear();
        }
    }

    private sealed class PidLockHandle : IDisposable
    {
        private readonly Action _release;

        public PidLockHandle(Action release)
        {
            _release = release;
        }

        public void Dispose()
        {
            _release();
        }
    }
}
