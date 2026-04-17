using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using Graphs;
using Microsoft.Diagnostics.Tools.GCDump;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// GcDump graph adapter using vendored GCHeapDump from dotnet/diagnostics.
/// 
/// NOTE: This adapter is for unit testing only. It loads gcdump files in-process
/// without the isolation provided by GcDumpProcessAdapter. For production use,
/// prefer GcDumpProcessAdapter which runs parsing in a separate process for
/// better isolation and error handling.
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
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogInformation("Step 1: Creating GCHeapDump from file");
                var gcHeapDump = new GCHeapDump(gcdumpPath);
                
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogInformation("Step 2: GCHeapDump created successfully");
                _logger.LogInformation("Step 3: Starting conversion to HeapGraphData");
                var result = ConvertToHeapGraphData(gcHeapDump, cancellationToken);
                
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogInformation("Step 4: Conversion completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load heap graph from {GcDumpPath}", gcdumpPath);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<HeapGraphData> LoadGraphAsync(Stream gcdumpStream, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading heap graph from stream");

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gcHeapDump = new GCHeapDump(gcdumpStream, "stream");
            return ConvertToHeapGraphData(gcHeapDump, cancellationToken);
        }, cancellationToken);
    }

    private HeapGraphData ConvertToHeapGraphData(GCHeapDump gcHeapDump, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("Step 3.1: Getting MemoryGraph from GCHeapDump");
        var graph = gcHeapDump.MemoryGraph;
        _logger.LogInformation("Step 3.2: MemoryGraph obtained, NodeIndexLimit: {NodeIndexLimit}", graph.NodeIndexLimit);

        var nodes = new Dictionary<long, MemoryNodeData>();
        var edges = new List<MemoryEdgeData>();
        var roots = new List<RootGroupData>();

        _logger.LogInformation("Step 3.3: Allocating node and type storage");
        var nodeStorage = graph.AllocNodeStorage();
        var typeStorage = graph.AllocTypeNodeStorage();

        int nodesWithSize = 0;
        int nodesWithoutSize = 0;
        int nodesTotal = 0;

        _logger.LogInformation("Step 3.4: Starting node iteration");
        for (NodeIndex idx = 0; (long)idx < (long)graph.NodeIndexLimit; idx++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            nodesTotal++;
            var node = graph.GetNode(idx, nodeStorage);
            if (node.Size == 0)
            {
                nodesWithoutSize++;
                continue;
            }

            nodesWithSize++;
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

        _logger.LogInformation("Step 3.5: Node iteration completed. Total: {Total}, WithSize: {WithSize}, WithoutSize: {WithoutSize}",
            nodesTotal, nodesWithSize, nodesWithoutSize);

        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("Step 3.6: Starting edge iteration");
        for (NodeIndex idx = 0; (long)idx < (long)graph.NodeIndexLimit; idx++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
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
        _logger.LogInformation("Step 3.7: Edge iteration completed, {EdgeCount} edges", edges.Count);

        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("Step 3.8: Starting root iteration");
        var rootNode = graph.GetNode(graph.RootIndex, nodeStorage);
        for (NodeIndex childIdx = rootNode.GetFirstChildIndex();
             childIdx != NodeIndex.Invalid;
             childIdx = rootNode.GetNextChildIndex())
        {
            cancellationToken.ThrowIfCancellationRequested();
            
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
        _logger.LogInformation("Step 3.9: Root iteration completed, {RootCount} roots", roots.Count);

        _logger.LogInformation("Step 3.10: Parsed {NodeCount} nodes with size, {NodesWithoutSize} nodes without size, {EdgeCount} edges, {RootCount} roots",
            nodesWithSize, nodesWithoutSize, edges.Count, roots.Count);

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
