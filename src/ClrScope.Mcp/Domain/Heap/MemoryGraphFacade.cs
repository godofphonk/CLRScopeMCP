namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Facade for reading MemoryGraph without direct dependency on Graphs/PerfView API.
/// </summary>
public interface IMemoryGraphFacade
{
    IReadOnlyList<GraphNodeRecord> GetNodes(object memoryGraph);
    IReadOnlyList<GraphEdgeRecord> GetEdges(object memoryGraph);
    IReadOnlyList<GraphRootRecord> GetRoots(object memoryGraph);
}

/// <summary>
/// Graph node record from MemoryGraph.
/// </summary>
public sealed class GraphNodeRecord
{
    public long NodeId { get; init; }
    public ulong? Address { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public long ShallowSizeBytes { get; init; }
    public int Count { get; init; } = 1;
}

/// <summary>
/// Graph edge record from MemoryGraph.
/// </summary>
public sealed class GraphEdgeRecord
{
    public long FromNodeId { get; init; }
    public long ToNodeId { get; init; }
    public bool IsWeak { get; init; }
    public string EdgeKind { get; init; } = "reference";
}

/// <summary>
/// Graph root record from MemoryGraph.
/// </summary>
public sealed class GraphRootRecord
{
    public long RootNodeId { get; init; }
    public string RootKind { get; init; } = "other";
}
