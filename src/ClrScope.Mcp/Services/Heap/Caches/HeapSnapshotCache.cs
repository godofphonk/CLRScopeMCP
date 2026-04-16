using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Domain.Heap.Options;
using System.Collections.Concurrent;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Cache for heap snapshot preparation results.
/// </summary>
public interface IHeapSnapshotCache
{
    string GenerateCacheKey(Artifact artifact, HeapPreparationOptions options);
    bool TryGet(string cacheKey, out HeapSnapshotData? snapshot);
    void Set(string cacheKey, HeapSnapshotData snapshot);
    void Clear();
}

/// <summary>
/// In-memory cache for heap snapshot preparation results.
/// </summary>
public sealed class HeapSnapshotCache : IHeapSnapshotCache
{
    private readonly ConcurrentDictionary<string, HeapSnapshotData> _cache = new();

    public string GenerateCacheKey(Artifact artifact, HeapPreparationOptions options)
    {
        var fingerprint = artifact.FilePath ?? artifact.ArtifactId.Value;
        return $"heap:{fingerprint}:metric={options.Metric}:group={options.GroupBy}:max={options.MaxTypes}:v=1";
    }

    public bool TryGet(string cacheKey, out HeapSnapshotData? snapshot)
    {
        return _cache.TryGetValue(cacheKey, out snapshot);
    }

    public void Set(string cacheKey, HeapSnapshotData snapshot)
    {
        _cache[cacheKey] = snapshot;
    }

    public void Clear()
    {
        _cache.Clear();
    }
}
