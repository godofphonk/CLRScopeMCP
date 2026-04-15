using ClrScope.Mcp.Domain.Artifacts;

namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Heap snapshot data structure for GcDump artifacts.
/// Full version with complete graph data.
/// </summary>
public sealed class HeapSnapshotData
{
    public required Artifact Artifact { get; init; }
    public required HeapMetadata Metadata { get; init; }

    // Complete graph data
    public required IReadOnlyList<MemoryNodeData> Nodes { get; init; }
    public required IReadOnlyList<MemoryEdgeData> Edges { get; init; }
    public required IReadOnlyList<RootGroupData> Roots { get; init; }

    // Aggregated type statistics
    public required IReadOnlyList<TypeStatData> TypeStats { get; init; }

    // Post-processing results
    public required Dictionary<long, long?> Dominators { get; init; }
    public required Dictionary<long, long> RetainedSizes { get; init; }
    public required Dictionary<long, int> Depths { get; init; }
}

/// <summary>
/// Metadata about the heap snapshot.
/// </summary>
public sealed class HeapMetadata
{
    public string RuntimeVersion { get; init; } = string.Empty;
    public string ToolVersion { get; init; } = string.Empty;
    public long TotalHeapBytes { get; init; }
    public long TotalObjectCount { get; init; }
    public int RootCount { get; init; }
    public int SegmentCount { get; init; }
    public bool IsPartial { get; init; }
    public string? Warning { get; init; }
}

/// <summary>
/// Type-level statistics aggregated from heap snapshot.
/// </summary>
public sealed class TypeStatData
{
    public string TypeName { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public string Generation { get; init; } = "mixed";

    public int Count { get; init; }
    public long ShallowSizeBytes { get; init; }
    public long RetainedSizeBytes { get; init; }
}
