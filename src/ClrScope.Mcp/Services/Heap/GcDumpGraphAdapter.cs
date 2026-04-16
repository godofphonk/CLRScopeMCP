using ClrScope.Mcp.Domain.Heap;
using Graphs;
using Microsoft.Diagnostics.Tools.GCDump;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// GcDump graph adapter using vendored GCHeapDump from dotnet/diagnostics.
/// </summary>
public sealed class GcDumpGraphAdapter : IGcDumpGraphAdapter
{
    private readonly ILogger<GcDumpGraphAdapter> _logger;

    public GcDumpGraphAdapter(ILogger<GcDumpGraphAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<HeapGraphData> LoadGraphAsync(string gcdumpPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading heap graph from {GcDumpPath}", gcdumpPath);

        return await Task.Run(() =>
        {
            var gcHeapDump = new GCHeapDump(gcdumpPath);
            return ConvertToHeapGraphData(gcHeapDump);
        }, cancellationToken);
    }

    public async Task<HeapGraphData> LoadGraphAsync(Stream gcdumpStream, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading heap graph from stream");

        return await Task.Run(() =>
        {
            var gcHeapDump = new GCHeapDump(gcdumpStream, "stream");
            return ConvertToHeapGraphData(gcHeapDump);
        }, cancellationToken);
    }

    private HeapGraphData ConvertToHeapGraphData(GCHeapDump gcHeapDump)
    {
        var graph = gcHeapDump.MemoryGraph;
        var nodes = new Dictionary<long, MemoryNodeData>();
        var edges = new List<MemoryEdgeData>();
        var roots = new List<RootGroupData>();

        var nodeStorage = graph.AllocNodeStorage();
        var typeStorage = graph.AllocTypeNodeStorage();

        for (NodeIndex idx = 0; (long)idx < (long)graph.NodeIndexLimit; idx++)
        {
            var node = graph.GetNode(idx, nodeStorage);
            if (node.Size == 0)
                continue;

            var nodeType = graph.GetType(node.TypeIndex, typeStorage);

            nodes[(long)idx] = new MemoryNodeData
            {
                NodeId = (long)idx,
                Address = null,
                TypeName = nodeType.Name ?? "Unknown",
                Namespace = string.Empty,
                AssemblyName = nodeType.ModuleName ?? string.Empty,
                ShallowSizeBytes = node.Size,
                RetainedSizeBytes = 0,
                Count = 1,
                Generation = "0",
                IsRoot = false,
                RootKind = null,
                DominatorNodeId = null
            };
        }

        for (NodeIndex idx = 0; (long)idx < (long)graph.NodeIndexLimit; idx++)
        {
            var node = graph.GetNode(idx, nodeStorage);
            var fromNodeId = (long)idx;

            for (NodeIndex childIdx = node.GetFirstChildIndex();
                 childIdx != NodeIndex.Invalid;
                 childIdx = node.GetNextChildIndex())
            {
                edges.Add(new MemoryEdgeData
                {
                    FromNodeId = fromNodeId,
                    ToNodeId = (long)childIdx,
                    EdgeKind = "reference",
                    IsWeak = false
                });
            }
        }

        var rootNode = graph.GetNode(graph.RootIndex, nodeStorage);
        for (NodeIndex childIdx = rootNode.GetFirstChildIndex();
             childIdx != NodeIndex.Invalid;
             childIdx = rootNode.GetNextChildIndex())
        {
            var childNode = graph.GetNode(childIdx, nodeStorage);
            var childType = graph.GetType(childNode.TypeIndex, typeStorage);

            string rootKind = MapTypeNameToRootKind(childType.Name);

            roots.Add(new RootGroupData
            {
                RootKind = rootKind,
                RootCount = 1,
                ReachableBytes = 0,
                RetainedBytes = 0
            });

            if (nodes.TryGetValue((long)childIdx, out var nodeData))
            {
                nodeData.IsRoot = true;
                nodeData.RootKind = rootKind;
            }
        }

        return new HeapGraphData
        {
            Nodes = nodes,
            Edges = edges,
            Roots = roots
        };
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
