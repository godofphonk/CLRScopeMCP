namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Serializes operations on a single PID using SemaphoreSlim with ref-count and idle TTL cleanup
/// </summary>
public class PidLockManager : IPidLockManager, IDisposable
{
    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount;
        public DateTime LastReleasedUtc;
    }

    private readonly Dictionary<int, LockEntry> _locks = new();
    private readonly object _syncRoot = new();
    private readonly TimeSpan _idleTtl = TimeSpan.FromMinutes(30);
    private int _releaseCount = 0;

    public async Task<IDisposable> AcquireLockAsync(int pid, CancellationToken cancellationToken = default)
    {
        LockEntry entry;

        lock (_syncRoot)
        {
            if (!_locks.TryGetValue(pid, out entry!))
            {
                entry = new LockEntry();
                _locks[pid] = entry;
            }
            entry.RefCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return new PidLockHandle(() => ReleaseLock(pid, entry));
        }
        catch
        {
            lock (_syncRoot)
            {
                entry.RefCount--;
                entry.LastReleasedUtc = DateTime.UtcNow;
            }
            throw;
        }
    }

    private void ReleaseLock(int pid, LockEntry entry)
    {
        lock (_syncRoot)
        {
            entry.RefCount--;
            entry.LastReleasedUtc = DateTime.UtcNow;
            _releaseCount++;

            // Opportunistic sweep every 10 release operations
            if (_releaseCount % 10 == 0)
            {
                SweepIdleLocks();
            }
        }

        entry.Semaphore.Release();
    }

    private void SweepIdleLocks()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = new List<int>();

        foreach (var kvp in _locks)
        {
            if (kvp.Value.RefCount == 0 && (now - kvp.Value.LastReleasedUtc) > _idleTtl)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            var entry = _locks[key];
            entry.Semaphore.Dispose();
            _locks.Remove(key);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            foreach (var entry in _locks.Values)
            {
                entry.Semaphore.Dispose();
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
