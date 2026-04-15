namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// View kind for heap snapshot visualization.
/// </summary>
public enum HeapViewKind
{
    TypeDistribution,
    Treemap,
    RetainedFlame,
    Diff,
    RetainerPaths
}

/// <summary>
/// Heap metric kind for aggregation.
/// </summary>
public enum HeapMetricKind
{
    ShallowSize,
    Count,
    RetainedSize
}

/// <summary>
/// Heap grouping strategy.
/// </summary>
public enum HeapGroupBy
{
    Type,
    Namespace,
    Assembly
}

/// <summary>
/// Heap analysis mode.
/// </summary>
public enum HeapAnalysisMode
{
    Auto,
    Reuse,
    Force
}
