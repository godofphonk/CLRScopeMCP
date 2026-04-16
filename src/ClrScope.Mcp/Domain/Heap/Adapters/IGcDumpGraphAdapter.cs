using ClrScope.Mcp.Domain.Heap.Data;

namespace ClrScope.Mcp.Domain.Heap.Adapters;

/// <summary>
/// Adapter for reading GcDump graph data.
/// </summary>
public interface IGcDumpGraphAdapter
{
    Task<HeapGraphData> LoadGraphAsync(string gcdumpPath, CancellationToken cancellationToken);
    Task<HeapGraphData> LoadGraphAsync(Stream gcdumpStream, CancellationToken cancellationToken);
}
