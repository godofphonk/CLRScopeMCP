using ClrScope.Mcp.Domain.Heap;
using Graphs;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Facade for reading MemoryGraph from PerfView/Graphs API.
/// This isolates CLRScope from unstable MemoryGraph API details.
/// </summary>
public sealed class PerfViewMemoryGraphFacade : IMemoryGraphFacade
{
    private readonly ILogger<PerfViewMemoryGraphFacade> _logger;

    public PerfViewMemoryGraphFacade(ILogger<PerfViewMemoryGraphFacade> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<GraphNodeRecord> GetNodes(object memoryGraph)
    {
        if (memoryGraph is not Graphs.MemoryGraph graph)
        {
            _logger.LogError("PerfViewMemoryGraphFacade.GetNodes: memoryGraph is not MemoryGraph");
            return new List<GraphNodeRecord>();
        }

        var nodes = new List<GraphNodeRecord>();
        var nodeStorage = graph.AllocNodeStorage();
        var typeStorage = graph.AllocTypeNodeStorage();

        for (NodeIndex idx = 0; (long)idx < (long)graph.NodeIndexLimit; idx++)
        {
            var node = graph.GetNode(idx, nodeStorage);
            if (node.Size == 0)
                continue;

            var nodeType = graph.GetType(node.TypeIndex, typeStorage);
            if (nodeType == null)
            {
                _logger.LogWarning("PerfViewMemoryGraphFacade.GetNodes: nodeType is null for NodeIndex {NodeIndex}", idx);
                continue;
            }

            nodes.Add(new GraphNodeRecord
            {
                NodeId = (long)idx,
                Address = null, // MemoryGraph doesn't expose addresses directly
                TypeName = nodeType.Name ?? string.Empty,
                AssemblyName = nodeType.ModuleName ?? string.Empty,
                ShallowSizeBytes = node.Size,
                Count = 1
            });
        }

        return nodes;
    }

    public IReadOnlyList<GraphEdgeRecord> GetEdges(object memoryGraph)
    {
        if (memoryGraph is not Graphs.MemoryGraph graph)
        {
            _logger.LogError("PerfViewMemoryGraphFacade.GetEdges: memoryGraph is not MemoryGraph");
            return new List<GraphEdgeRecord>();
        }

        var edges = new List<GraphEdgeRecord>();
        var nodeStorage = graph.AllocNodeStorage();

        for (NodeIndex idx = 0; (long)idx < (long)graph.NodeIndexLimit; idx++)
        {
            var node = graph.GetNode(idx, nodeStorage);
            var fromNodeId = (long)idx;

            for (NodeIndex childIdx = node.GetFirstChildIndex();
                 childIdx != NodeIndex.Invalid;
                 childIdx = node.GetNextChildIndex())
            {
                edges.Add(new GraphEdgeRecord
                {
                    FromNodeId = fromNodeId,
                    ToNodeId = (long)childIdx,
                    IsWeak = false,
                    EdgeKind = "reference"
                });
            }
        }

        return edges;
    }

    public IReadOnlyList<GraphRootRecord> GetRoots(object memoryGraph)
    {
        if (memoryGraph is not Graphs.MemoryGraph graph)
        {
            _logger.LogError("PerfViewMemoryGraphFacade.GetRoots: memoryGraph is not MemoryGraph");
            return new List<GraphRootRecord>();
        }

        var roots = new List<GraphRootRecord>();
        var nodeStorage = graph.AllocNodeStorage();
        var typeStorage = graph.AllocTypeNodeStorage();

        // MemoryGraph has a synthetic root at graph.RootIndex
        var rootNode = graph.GetNode(graph.RootIndex, nodeStorage);
        if (rootNode == null)
        {
            _logger.LogWarning("PerfViewMemoryGraphFacade.GetRoots: rootNode is null");
            return roots;
        }

        // Iterate children of root to get root set
        for (NodeIndex childIdx = rootNode.GetFirstChildIndex();
             childIdx != NodeIndex.Invalid;
             childIdx = rootNode.GetNextChildIndex())
        {
            var childNode = graph.GetNode(childIdx, nodeStorage);
            if (childNode == null)
            {
                _logger.LogWarning("PerfViewMemoryGraphFacade.GetRoots: childNode is null for NodeIndex {ChildIndex}", childIdx);
                continue;
            }

            var childType = graph.GetType(childNode.TypeIndex, typeStorage);
            if (childType == null)
            {
                _logger.LogWarning("PerfViewMemoryGraphFacade.GetRoots: childType is null for NodeIndex {ChildIndex}", childIdx);
                continue;
            }

            // Determine root kind based on type name
            string rootKind = MapTypeNameToRootKind(childType.Name);

            roots.Add(new GraphRootRecord
            {
                RootNodeId = (long)childIdx,
                RootKind = rootKind
            });
        }

        return roots;
    }

    private static string MapTypeNameToRootKind(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "other";

        if (typeName.Contains("Static") || typeName.Contains("StaticVariables"))
            return "static";
        if (typeName.Contains("Handle") || typeName.Contains("DependentHandle"))
            return "handle";
        if (typeName.Contains("COM"))
            return "com";
        if (typeName.Contains("Finalizer") || typeName.Contains("FinalizationQueue"))
            return "finalizer";

        return "other";
    }
}
