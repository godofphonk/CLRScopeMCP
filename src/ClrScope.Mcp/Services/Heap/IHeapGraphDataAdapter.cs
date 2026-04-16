using ClrScope.Mcp.Domain.Heap;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Adapter for loading heap graph data from various sources (.gcdump, .nettrace).
/// Returns HeapGraphData directly without MemoryGraph conversion.
/// </summary>
public interface IHeapGraphDataAdapter
{
    /// <summary>
    /// Load heap graph data from file.
    /// </summary>
    Task<HeapGraphData> LoadGraphAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
