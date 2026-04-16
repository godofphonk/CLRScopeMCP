using System.Text.Json;
using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using Graphs;
using Microsoft.Diagnostics.Tools.GCDump;

namespace ClrScope.HeapParser;

class NettraceProbeResult
{
    public bool HasHeapSnapshotEvents { get; set; }
    public bool HasGCBulkNode { get; set; }
    public bool HasGCBulkEdge { get; set; }
    public bool HasGCBulkType { get; set; }
    public bool HasGCBulkRoot { get; set; }
    public bool RuntimeProviderSeen { get; set; }
    public bool IsFullHeapGraphCapable { get; set; }
    public string Mode { get; set; } = "no-heap-data"; // no-heap-data, partial-heap-data, full-heap-graph
    public List<string> RecommendedViews { get; set; } = new();
    public List<string> UnsupportedViews { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ClrScope.HeapParser <gcdump|nettrace|probe-nettrace> <filepath>");
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
            else if (mode == "probe-nettrace")
            {
                await ProbeNettrace(filePath);
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

                // Only if fromNode exists in nodes
                if (!nodes.ContainsKey(fromNodeId))
                    continue;

                for (NodeIndex childIdx = node.GetFirstChildIndex();
                     childIdx != NodeIndex.Invalid;
                     childIdx = node.GetNextChildIndex())
                {
                    // Only if toNode exists in nodes
                    if (nodes.ContainsKey((long)childIdx))
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
            var memoryGraph = new MemoryGraph(10000); // Official reader uses 10000
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
            int totalNodes = 0;
            int skippedSizeZero = 0;
            int skippedNullRef = 0;
            
            for (NodeIndex idx = 0; (long)idx < (long)memoryGraph.NodeIndexLimit; idx++)
            {
                totalNodes++;
                var node = memoryGraph.GetNode(idx, nodeStorage);
                long size = 0;
                try
                {
                    size = node.Size;
                }
                catch (NullReferenceException ex)
                {
                    // This indicates a problem with the MemoryGraph or vendored library
                    // Log it and continue, but this is not a normal scenario
                    skippedNullRef++;
                    Console.Error.WriteLine($"ParseNettrace: NullReferenceException at node {idx}: {ex.Message}");
                    continue; // Skip this node - it's corrupted
                }

                if (size == 0)
                {
                    skippedSizeZero++;
                    continue; // Skip zero-size nodes for now
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

            Console.Error.WriteLine($"ParseNettrace: Total nodes={totalNodes}, Skipped size=0={skippedSizeZero}, Skipped NullRef={skippedNullRef}, Added={nodes.Count}");

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

    static int ExtractCount(string log, string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(log, $@"{key}[:\s]+(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
        {
            return count;
        }
        return 0;
    }

    static async Task ProbeNettrace(string filePath)
    {
        await Task.Run(() =>
        {
            var result = new NettraceProbeResult
            {
                HasHeapSnapshotEvents = false,
                HasGCBulkNode = false,
                HasGCBulkEdge = false,
                HasGCBulkType = false,
                HasGCBulkRoot = false,
                RuntimeProviderSeen = false,
                IsFullHeapGraphCapable = false,
                Mode = "no-heap-data",
                RecommendedViews = new List<string>(),
                UnsupportedViews = new List<string>(),
                Message = string.Empty
            };

            try
            {
                var memoryGraph = new MemoryGraph(1000); // Small initial size for probe
                var dotNetHeapInfo = new DotNetHeapInfo();

                using var logWriter = new StringWriter();
                
                // Try to parse the trace with EventPipeDotNetHeapDumper
                var success = EventPipeDotNetHeapDumper.DumpFromEventPipeFile(
                    filePath,
                    memoryGraph,
                    logWriter,
                    dotNetHeapInfo);

                if (!success)
                {
                    result.Message = "Failed to parse EventPipe trace: " + logWriter.ToString();
                    result.Mode = "no-heap-data";
                    result.UnsupportedViews.AddRange(new[] { "type_distribution", "retained_flame", "retainer_paths", "diff", "treemap" });
                }
                else
                {
                    result.RuntimeProviderSeen = true;
                    
                    if (memoryGraph.NodeIndexLimit > 0)
                    {
                        result.HasHeapSnapshotEvents = true;
                        result.HasGCBulkNode = true; // If we have nodes, we have GCBulkNode events
                        
                        // Extract log output to check for BulkTypeEventCount, BulkNodeEventCount, BulkEdgeEventCount
                        var logOutput = logWriter.ToString();
                        Console.Error.WriteLine("ProbeNettrace log output:\n" + logOutput);
                        
                        // Parse actual counts from log output
                        var bulkTypeCount = ExtractCount(logOutput, "BulkTypeEventCount");
                        var bulkNodeCount = ExtractCount(logOutput, "BulkNodeEventCount");
                        var bulkEdgeCount = ExtractCount(logOutput, "BulkEdgeEventCount");
                        
                        Console.Error.WriteLine($"ProbeNettrace counts: BulkType={bulkTypeCount}, BulkNode={bulkNodeCount}, BulkEdge={bulkEdgeCount}");
                        
                        // Check for completion boundary (reader logs errors if incomplete)
                        var hasCompletion = !logOutput.Contains("start of a heap dump but not its completion") &&
                                            !logOutput.Contains("not enough edge data") &&
                                            !logOutput.Contains("Giving up on heap dump");
                        
                        Console.Error.WriteLine($"ProbeNettrace hasCompletion: {hasCompletion}");
                        
                        // Check for type/edge/root events in log (reader logs these counts)
                        var hasTypeEvents = bulkTypeCount > 0;
                        var hasEdgeEvents = bulkEdgeCount > 0;
                        var hasRootEvents = logOutput.Contains("GCHeapRoot") || logOutput.Contains("BulkRoot");
                        
                        result.HasGCBulkType = hasTypeEvents;
                        result.HasGCBulkEdge = hasEdgeEvents;
                        result.HasGCBulkRoot = hasRootEvents;
                        
                        // Classify: NoHeapData, PartialHeapData, or FullHeapGraphData
                        // Check if BulkNodeEventCount is reasonable compared to NodeIndexLimit
                        // If BulkNodeEventCount is very small but NodeIndexLimit is large, this indicates partial/inconsistent data
                        var isConsistentNodeCount = bulkNodeCount == 0 || bulkNodeCount >= (long)memoryGraph.NodeIndexLimit * 0.1; // At least 10% of nodes should have bulk events
                        
                        if (!result.HasGCBulkNode)
                        {
                            result.Mode = "no-heap-data";
                            result.Message = "Trace contains no heap snapshot events";
                            result.UnsupportedViews.AddRange(new[] { "type_distribution", "retained_flame", "retainer_paths", "diff", "treemap" });
                        }
                        else if (!result.HasGCBulkType || !result.HasGCBulkEdge || !hasCompletion || !isConsistentNodeCount)
                        {
                            result.Mode = "partial-heap-data";
                            result.IsFullHeapGraphCapable = false;
                            var missingParts = new List<string>();
                            if (!result.HasGCBulkType) missingParts.Add("GCBulkType");
                            if (!result.HasGCBulkEdge) missingParts.Add("GCBulkEdge");
                            if (!hasCompletion) missingParts.Add("completion boundary");
                            if (!isConsistentNodeCount) missingParts.Add($"consistent node count (BulkNodeEvents: {bulkNodeCount} vs NodeIndexLimit: {memoryGraph.NodeIndexLimit})");
                            
                            result.Message = $"Trace contains partial heap data (GCBulkNode: {memoryGraph.NodeIndexLimit}, BulkType: {bulkTypeCount}, BulkEdge: {bulkEdgeCount}, Completion: {hasCompletion}). " +
                                           $"Missing: {string.Join(", ", missingParts)}. Only type distribution is supported. For full heap graph, collect trace with complete heap snapshot events.";
                            result.RecommendedViews.Add("type_distribution");
                            result.UnsupportedViews.AddRange(new[] { "retained_flame", "retainer_paths", "diff", "treemap" });
                        }
                        else
                        {
                            result.Mode = "full-heap-graph";
                            result.IsFullHeapGraphCapable = true;
                            result.Message = $"Trace contains full heap snapshot data with {memoryGraph.NodeIndexLimit} nodes (BulkType: {bulkTypeCount}, BulkEdge: {bulkEdgeCount})";
                            result.RecommendedViews.AddRange(new[] { "type_distribution", "retained_flame", "retainer_paths", "treemap", "diff" });
                        }
                    }
                    else
                    {
                        result.Mode = "no-heap-data";
                        result.Message = "Trace was parsed successfully but contains no heap snapshot events. " +
                                       "Collect trace with explicit runtime heap-snapshot provider/keywords " +
                                       "instead of default dotnet-trace profiles.";
                        result.UnsupportedViews.AddRange(new[] { "type_distribution", "retained_flame", "retainer_paths", "diff", "treemap" });
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Error probing trace: {ex.Message}";
                result.Mode = "no-heap-data";
                result.UnsupportedViews.AddRange(new[] { "type_distribution", "retained_flame", "retainer_paths", "diff", "treemap" });
            }

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
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
