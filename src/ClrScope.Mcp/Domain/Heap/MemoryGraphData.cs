namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Memory node in heap graph.
/// </summary>
public sealed class MemoryNodeData
{
    public required string ObjectId { get; set; }
    public required string TypeName { get; set; }
    public required string Namespace { get; set; }
    public required string AssemblyName { get; set; }
    public required long ShallowSizeBytes { get; set; }
    public required long RetainedSizeBytes { get; set; }
    public required int Count { get; set; }
    public required string Generation { get; set; }
    public required List<string> References { get; set; } = new();
    public required List<string> ReferencedBy { get; set; } = new();
}

/// <summary>
/// Memory edge in heap graph.
/// </summary>
public sealed class MemoryEdgeData
{
    public required string FromObjectId { get; set; }
    public required string ToObjectId { get; set; }
    public required string FieldName { get; set; }
}

/// <summary>
/// Root group in heap graph.
/// </summary>
public sealed class RootGroupData
{
    public required string RootId { get; set; }
    public required string RootName { get; set; }
    public required string RootKind { get; set; }
    public required List<string> RootedObjectIds { get; set; } = new();
}

/// <summary>
/// Complete heap graph data.
/// </summary>
public sealed class HeapGraphData
{
    public required Dictionary<string, MemoryNodeData> Nodes { get; set; }
    public required List<MemoryEdgeData> Edges { get; set; }
    public required List<RootGroupData> Roots { get; set; }
}
