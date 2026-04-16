using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Domain.Heap.Enums;

namespace ClrScope.Mcp.Domain.Heap.Options;

/// <summary>
/// Options for heap snapshot preparation.
/// </summary>
public sealed class HeapPreparationOptions
{
    public HeapMetricKind Metric { get; init; } = HeapMetricKind.ShallowSize;
    public HeapAnalysisMode AnalysisMode { get; init; } = HeapAnalysisMode.Auto;
    public HeapGroupBy GroupBy { get; init; } = HeapGroupBy.Type;
    public int MaxTypes { get; init; } = 200;
}

/// <summary>
/// Result of heap snapshot preparation.
/// </summary>
public sealed class PreparedHeapVisualizationData
{
    public required HeapSnapshotData Snapshot { get; init; }
    public required HeapViewKind SuggestedDefaultView { get; init; }
    public required bool FromCache { get; init; }
    public required string CacheKey { get; init; }
}
