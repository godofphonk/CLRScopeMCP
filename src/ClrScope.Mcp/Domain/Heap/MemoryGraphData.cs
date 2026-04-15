namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Memory node in heap graph.
/// </summary>
public sealed class MemoryNodeData
{
    public required long NodeId { get; set; }
    public ulong? Address { get; set; }
    public required string TypeName { get; set; }
    public required string Namespace { get; set; }
    public required string AssemblyName { get; set; }
    public required long ShallowSizeBytes { get; set; }
    public required long RetainedSizeBytes { get; set; }
    public required int Count { get; set; }
    public required string Generation { get; set; }
    public bool IsRoot { get; set; }
    public string? RootKind { get; set; }
    public long? DominatorNodeId { get; set; }
}

/// <summary>
/// Memory edge in heap graph.
/// </summary>
public sealed class MemoryEdgeData
{
    public required long FromNodeId { get; set; }
    public required long ToNodeId { get; set; }
    public required string EdgeKind { get; set; }
    public bool IsWeak { get; set; }
}

/// <summary>
/// Root group in heap graph.
/// </summary>
public sealed class RootGroupData
{
    public required string RootKind { get; set; }
    public required int RootCount { get; set; }
    public required long ReachableBytes { get; set; }
    public required long RetainedBytes { get; set; }
}

/// <summary>
/// Complete heap graph data.
/// </summary>
public sealed class HeapGraphData
{
    public required Dictionary<long, MemoryNodeData> Nodes { get; set; }
    public required List<MemoryEdgeData> Edges { get; set; }
    public required List<RootGroupData> Roots { get; set; }
}
