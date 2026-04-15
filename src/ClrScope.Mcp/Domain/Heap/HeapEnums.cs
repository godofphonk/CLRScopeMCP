namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// View kind for heap snapshot visualization.
/// </summary>
public enum HeapViewKind
{
    TypeDistribution,
    Treemap,
    // Future views:
    // Sunburst,
    // RetainedFlame,
    // RetainerPaths,
    // Diff
}

/// <summary>
/// Metric kind for heap snapshot analysis.
/// </summary>
public enum HeapMetricKind
{
    ShallowSize,
    Count,
    // Future metrics:
    // RetainedSize
}

/// <summary>
/// Grouping options for heap snapshot visualization.
/// </summary>
public enum HeapGroupBy
{
    Type,
    Namespace,
    Assembly
}

/// <summary>
/// Analysis mode for heap snapshot.
/// </summary>
public enum HeapAnalysisMode
{
    Auto,
    Reuse,
    Force
}
