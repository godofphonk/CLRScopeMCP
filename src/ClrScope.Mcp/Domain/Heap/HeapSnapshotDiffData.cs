namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Heap snapshot diff data for comparing two snapshots.
/// </summary>
public sealed class HeapSnapshotDiffData
{
    public required HeapSnapshotData Baseline { get; init; }
    public required HeapSnapshotData Target { get; init; }
    public required List<TypeDiffData> TypeDiffs { get; init; }
}

/// <summary>
/// Type-level diff between two snapshots.
/// </summary>
public sealed class TypeDiffData
{
    public required string TypeName { get; init; }
    public required string Namespace { get; init; }
    public required string AssemblyName { get; init; }
    public required DiffStatus Status { get; init; }
    public required int BaselineCount { get; init; }
    public required int TargetCount { get; init; }
    public required int CountDelta { get; init; }
    public required long BaselineShallowSize { get; init; }
    public required long TargetShallowSize { get; init; }
    public required long ShallowSizeDelta { get; init; }
    public required double ShallowSizePercentChange { get; init; }
}

/// <summary>
/// Diff status for type comparison.
/// </summary>
public enum DiffStatus
{
    Added,
    Removed,
    Increased,
    Decreased,
    Changed,
    Unchanged
}
