using System.Text.Json;
using ClrScope.Mcp.Domain.Heap;
using Graphs;
using Microsoft.Diagnostics.Tools.GCDump;

namespace ClrScope.HeapParser;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ClrScope.HeapParser <gcdump|nettrace> <filepath>");
            Environment.Exit(1);
            return;
        }

        var mode = args[0];
        var filePath = args[1];

        try
        {
            if (mode == "gcdump")
            {
                await ParseGcDump(filePath);
            }
            else if (mode == "nettrace")
            {
                await ParseNettrace(filePath);
            }
            else
            {
                Console.Error.WriteLine($"Unknown mode: {mode}");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    static async Task ParseGcDump(string filePath)
    {
        var graphData = await LoadGcDumpGraph(filePath);
        var json = JsonSerializer.Serialize(graphData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Console.WriteLine(json);
    }

    static async Task<HeapGraphData> LoadGcDumpGraph(string gcdumpPath)
    {
        return await Task.Run(() =>
        {
            var gcHeapDump = new GCHeapDump(gcdumpPath);
            var graph = gcHeapDump.MemoryGraph;

            var nodes = new Dictionary<long, MemoryNodeData>();
            var edges = new List<MemoryEdgeData>();
            var roots = new List<RootGroupData>();

            var nodeStorage = graph.AllocNodeStorage();
            var typeStorage = graph.AllocTypeNodeStorage();

            int nodesWithSize = 0;
            int nodesWithoutSize = 0;

            for (NodeIndex idx = 0; (long)idx < (long)graph.NodeIndexLimit; idx++)
            {
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
        });
    }

    static async Task ParseNettrace(string filePath)
    {
        // EventPipeDotNetHeapDumper is synchronous and blocking
        // Running in separate process allows killing the process on timeout
        await Task.Run(() =>
        {
            var memoryGraph = new MemoryGraph(100000);
            var dotNetHeapInfo = new DotNetHeapInfo();

            using var logWriter = new StringWriter();
            var success = EventPipeDotNetHeapDumper.DumpFromEventPipeFile(
                filePath,
                memoryGraph,
                logWriter,
                dotNetHeapInfo);

            if (!success)
            {
                throw new InvalidOperationException("Failed to parse EventPipe trace");
            }

            if (memoryGraph.NodeIndexLimit == 0)
            {
                throw new InvalidOperationException("No heap data found in EventPipe trace");
            }

            // Convert to simplified JSON output
            var nodeStorage = memoryGraph.AllocNodeStorage();
            var typeStorage = memoryGraph.AllocTypeNodeStorage();

            var nodes = new Dictionary<long, MemoryNodeData>();
            
            for (NodeIndex idx = 0; (long)idx < (long)memoryGraph.NodeIndexLimit; idx++)
            {
                var node = memoryGraph.GetNode(idx, nodeStorage);
                long size = 0;
                try
                {
                    size = node.Size;
                }
                catch (NullReferenceException)
                {
                    // Vendored library bug: Node.get_Size() can throw for some nodes
                    continue;
                }

                if (size == 0)
                {
                    continue;
                }

                var nodeType = memoryGraph.GetType(node.TypeIndex, typeStorage);

                nodes[(long)idx] = new MemoryNodeData
                {
                    NodeId = (long)idx,
                    Address = null,
                    TypeName = nodeType.Name ?? "Unknown",
                    Namespace = string.Empty,
                    AssemblyName = nodeType.ModuleName ?? string.Empty,
                    ShallowSizeBytes = size,
                    RetainedSizeBytes = 0,
                    Count = 1,
                    Generation = "0",
                    IsRoot = false,
                    RootKind = null,
                    DominatorNodeId = null
                };
            }

            var graphData = new HeapGraphData
            {
                Nodes = nodes,
                Edges = new List<MemoryEdgeData>(),
                Roots = new List<RootGroupData>()
            };

            var json = JsonSerializer.Serialize(graphData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            Console.WriteLine(json);
        });
    }

    static string MapTypeNameToRootKind(string? typeName)
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
