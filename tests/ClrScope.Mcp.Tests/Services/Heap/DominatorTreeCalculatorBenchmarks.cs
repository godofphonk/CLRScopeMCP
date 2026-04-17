using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Tests.Services.Heap;

/// <summary>
/// Benchmarks for DominatorTreeCalculator operations.
/// Run with: dotnet run -c Release --project tests/ClrScope.Mcp.Tests/ClrScope.Mcp.Tests.csproj --filter "*DominatorTreeCalculator*"
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[Config(typeof(Config))]
public class DominatorTreeCalculatorBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            SummaryStyle = BenchmarkDotNet.Reports.SummaryStyle.Default
                .WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Percentage);
        }
    }

    private GcDumpGraphAdapter _adapter = null!;
    private DominatorTreeCalculator _calculator = null!;
    private HeapGraphData _graph = null!;
    private string _gcdumpPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Error);
        });

        var adapterLogger = loggerFactory.CreateLogger<GcDumpGraphAdapter>();
        var calculatorLogger = loggerFactory.CreateLogger<DominatorTreeCalculator>();

        _adapter = new GcDumpGraphAdapter(adapterLogger);
        _calculator = new DominatorTreeCalculator(calculatorLogger);

        _gcdumpPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
        
        // Load graph once for all benchmarks
        _graph = _adapter.LoadGraphAsync(_gcdumpPath, CancellationToken.None).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _graph = null!;
        _adapter = null!;
        _calculator = null!;
    }

    [Benchmark]
    [BenchmarkCategory("FullPipeline")]
    public void CalculateRetainedSize_FullPipeline()
    {
        // Create a copy of the graph for each benchmark iteration
        var graphCopy = CloneGraph(_graph);
        _calculator.CalculateRetainedSize(graphCopy);
    }

    [Benchmark]
    [BenchmarkCategory("IndividualSteps")]
    public void BuildSuperRoot()
    {
        // Build super-root adjacency list
        var adjacencyList = new Dictionary<long, List<long>>();
        
        foreach (var node in _graph.Nodes.Values)
        {
            adjacencyList[node.NodeId] = new List<long>();
        }

        const long superRootNodeId = -1;
        adjacencyList[superRootNodeId] = new List<long>();

        var rootNodes = _graph.Nodes.Values.Where(n => n.IsRoot).ToList();
        foreach (var node in rootNodes)
        {
            adjacencyList[superRootNodeId].Add(node.NodeId);
        }

        foreach (var edge in _graph.Edges)
        {
            if (!adjacencyList.ContainsKey(edge.ToNodeId))
            {
                continue;
            }

            if (!adjacencyList.ContainsKey(edge.FromNodeId))
            {
                adjacencyList[edge.FromNodeId] = new List<long>();
            }
            adjacencyList[edge.FromNodeId].Add(edge.ToNodeId);
        }
    }

    [Benchmark]
    [BenchmarkCategory("IndividualSteps")]
    public void FilterWeakEdges()
    {
        // Build adjacency list first
        var adjacencyList = new Dictionary<long, List<long>>();
        
        foreach (var node in _graph.Nodes.Values)
        {
            adjacencyList[node.NodeId] = new List<long>();
        }

        const long superRootNodeId = -1;
        adjacencyList[superRootNodeId] = new List<long>();

        var rootNodes = _graph.Nodes.Values.Where(n => n.IsRoot).ToList();
        foreach (var node in rootNodes)
        {
            adjacencyList[superRootNodeId].Add(node.NodeId);
        }

        foreach (var edge in _graph.Edges)
        {
            if (!adjacencyList.ContainsKey(edge.ToNodeId))
            {
                continue;
            }

            if (!adjacencyList.ContainsKey(edge.FromNodeId))
            {
                adjacencyList[edge.FromNodeId] = new List<long>();
            }
            adjacencyList[edge.FromNodeId].Add(edge.ToNodeId);
        }

        // Now filter weak edges
        var weakEdges = new HashSet<(long, long)>();
        foreach (var edge in _graph.Edges.Where(e => e.IsWeak))
        {
            weakEdges.Add((edge.FromNodeId, edge.ToNodeId));
        }

        var filteredAdjacency = new Dictionary<long, List<long>>();
        foreach (var (fromNodeId, toNodeIds) in adjacencyList)
        {
            var filteredToNodeIds = toNodeIds
                .Where(toNodeId => !weakEdges.Contains((fromNodeId, toNodeId)))
                .ToList();

            filteredAdjacency[fromNodeId] = filteredToNodeIds;
        }
    }

    [Benchmark]
    [BenchmarkCategory("IndividualSteps")]
    public void ComputeImmediateDominatorsCHK()
    {
        // This is a simplified benchmark of the CHK algorithm
        // In a real scenario, this would test the actual ComputeImmediateDominatorsCHK method
        // but since it's private, we benchmark a similar operation
        
        var adjacencyList = new Dictionary<long, List<long>>();
        
        foreach (var node in _graph.Nodes.Values)
        {
            adjacencyList[node.NodeId] = new List<long>();
        }

        const long superRootNodeId = -1;
        adjacencyList[superRootNodeId] = new List<long>();

        var rootNodes = _graph.Nodes.Values.Where(n => n.IsRoot).ToList();
        foreach (var node in rootNodes)
        {
            adjacencyList[superRootNodeId].Add(node.NodeId);
        }

        foreach (var edge in _graph.Edges.Where(e => !e.IsWeak))
        {
            if (!adjacencyList.ContainsKey(edge.ToNodeId))
            {
                continue;
            }

            if (!adjacencyList.ContainsKey(edge.FromNodeId))
            {
                adjacencyList[edge.FromNodeId] = new List<long>();
            }
            adjacencyList[edge.FromNodeId].Add(edge.ToNodeId);
        }

        // Postorder DFS
        var postorder = new Dictionary<long, long>();
        var postorderList = new List<long>();
        var visited = new HashSet<long>();
        long postNum = 0;

        void PostorderDFS(long u)
        {
            var stack = new Stack<(long node, int childIndex)>();
            visited.Add(u);
            stack.Push((u, 0));

            while (stack.Count > 0)
            {
                var (node, childIdx) = stack.Pop();
                var children = adjacencyList.GetValueOrDefault(node, new List<long>());

                if (childIdx < children.Count)
                {
                    stack.Push((node, childIdx + 1));
                    var child = children[childIdx];
                    if (!visited.Contains(child))
                    {
                        visited.Add(child);
                        stack.Push((child, 0));
                    }
                }
                else
                {
                    postorder[node] = postNum++;
                    postorderList.Add(node);
                }
            }
        }

        PostorderDFS(superRootNodeId);

        // Build reverse adjacency
        var reverseAdjacency = new Dictionary<long, List<long>>();
        foreach (var nodeId in visited)
        {
            reverseAdjacency[nodeId] = new List<long>();
        }
        foreach (var (from, toList) in adjacencyList)
        {
            if (!visited.Contains(from)) continue;
            foreach (var to in toList)
            {
                if (!visited.Contains(to)) continue;
                reverseAdjacency[to].Add(from);
            }
        }

        // Initialize dom array
        var dom = new Dictionary<long, long?>();
        foreach (var nodeId in visited)
        {
            dom[nodeId] = null;
        }
        dom[superRootNodeId] = superRootNodeId;

        // Intersect function
        long Intersect(long b1, long b2)
        {
            var finger1 = b1;
            var finger2 = b2;
            int safety = visited.Count + 1;
            while (finger1 != finger2 && safety-- > 0)
            {
                while (postorder[finger1] < postorder[finger2] && safety-- > 0)
                {
                    finger1 = dom[finger1]!.Value;
                }
                while (postorder[finger2] < postorder[finger1] && safety-- > 0)
                {
                    finger2 = dom[finger2]!.Value;
                }
            }
            return finger1;
        }

        // Process nodes in reverse postorder
        var reversePostorder = new List<long>(postorderList);
        reversePostorder.Reverse();

        bool changed;
        do
        {
            changed = false;

            foreach (var b in reversePostorder)
            {
                if (b == superRootNodeId) continue;

                var preds = reverseAdjacency.GetValueOrDefault(b, new List<long>());
                if (preds.Count == 0) continue;

                long newIdom = -2;
                int startIdx = 0;
                for (int i = 0; i < preds.Count; i++)
                {
                    if (dom[preds[i]] != null)
                    {
                        newIdom = preds[i];
                        startIdx = i + 1;
                        break;
                    }
                }
                if (newIdom == -2) continue;

                for (int i = startIdx; i < preds.Count; i++)
                {
                    if (dom[preds[i]] != null)
                    {
                        newIdom = Intersect(preds[i], newIdom);
                    }
                }

                if (dom[b] != newIdom)
                {
                    dom[b] = newIdom;
                    changed = true;
                }
            }
        } while (changed);
    }

    [Benchmark]
    [BenchmarkCategory("IndividualSteps")]
    public void FillDominatorNodeId()
    {
        // Simulate filling dominator node IDs
        var immediateDominators = new Dictionary<long, long?>();
        foreach (var node in _graph.Nodes.Values)
        {
            immediateDominators[node.NodeId] = node.NodeId > 0 ? node.NodeId - 1 : null;
        }

        foreach (var node in _graph.Nodes.Values)
        {
            node.DominatorNodeId = immediateDominators.GetValueOrDefault(node.NodeId);
        }
    }

    [Benchmark]
    [BenchmarkCategory("IndividualSteps")]
    public void AggregateRetainedSize()
    {
        // Simulate aggregating retained size
        var immediateDominators = new Dictionary<long, long?>();
        foreach (var node in _graph.Nodes.Values)
        {
            immediateDominators[node.NodeId] = node.NodeId > 0 ? node.NodeId - 1 : null;
        }

        // Build children map
        var children = new Dictionary<long, List<long>>();
        foreach (var node in _graph.Nodes.Values)
        {
            children[node.NodeId] = new List<long>();
        }

        const long superRootNodeId = -1;
        foreach (var (nodeId, dominatorId) in immediateDominators)
        {
            if (dominatorId.HasValue && dominatorId != superRootNodeId)
            {
                if (!children.ContainsKey(dominatorId.Value))
                {
                    children[dominatorId.Value] = new List<long>();
                }
                children[dominatorId.Value].Add(nodeId);
            }
        }

        // Post-order traversal
        var visited = new HashSet<long>();
        foreach (var nodeId in _graph.Nodes.Keys)
        {
            if (visited.Contains(nodeId)) continue;

            var stack = new Stack<(long node, int state)>();
            stack.Push((nodeId, 0));

            while (stack.Count > 0)
            {
                var (currentNode, state) = stack.Pop();

                if (state == 0)
                {
                    if (visited.Contains(currentNode)) continue;
                    visited.Add(currentNode);

                    if (!_graph.Nodes.ContainsKey(currentNode)) continue;

                    stack.Push((currentNode, 1));

                    var childList = children.GetValueOrDefault(currentNode, new List<long>());
                    for (int i = childList.Count - 1; i >= 0; i--)
                    {
                        stack.Push((childList[i], 0));
                    }
                }
                else
                {
                    if (!_graph.Nodes.ContainsKey(currentNode)) continue;

                    var node = _graph.Nodes[currentNode];
                    node.RetainedSizeBytes = node.ShallowSizeBytes;

                    foreach (var childId in children.GetValueOrDefault(currentNode, new List<long>()))
                    {
                        if (_graph.Nodes.ContainsKey(childId))
                        {
                            node.RetainedSizeBytes += _graph.Nodes[childId].RetainedSizeBytes;
                        }
                    }
                }
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("RetainerPaths")]
    public void FindRetainerPaths()
    {
        var graphCopy = CloneGraph(_graph);
        _calculator.CalculateRetainedSize(graphCopy);
        
        var targetNodeId = graphCopy.Nodes.Values.FirstOrDefault(n => !n.IsRoot)?.NodeId 
            ?? graphCopy.Nodes.Keys.First();
        
        _calculator.FindRetainerPaths(graphCopy, targetNodeId, maxPaths: 10);
    }

    [Benchmark]
    [BenchmarkCategory("Scalability")]
    public void MultipleCalculations()
    {
        for (int i = 0; i < 10; i++)
        {
            var graphCopy = CloneGraph(_graph);
            _calculator.CalculateRetainedSize(graphCopy);
        }
    }

    private HeapGraphData CloneGraph(HeapGraphData original)
    {
        var nodes = new Dictionary<long, MemoryNodeData>();
        foreach (var (nodeId, node) in original.Nodes)
        {
            nodes[nodeId] = new MemoryNodeData
            {
                NodeId = node.NodeId,
                TypeName = node.TypeName,
                Namespace = node.Namespace,
                AssemblyName = node.AssemblyName,
                ShallowSizeBytes = node.ShallowSizeBytes,
                RetainedSizeBytes = node.RetainedSizeBytes,
                Count = node.Count,
                Generation = node.Generation,
                IsRoot = node.IsRoot,
                RootKind = node.RootKind,
                DominatorNodeId = node.DominatorNodeId,
                Address = node.Address
            };
        }

        var edges = new List<MemoryEdgeData>(original.Edges.Count);
        foreach (var edge in original.Edges)
        {
            edges.Add(new MemoryEdgeData
            {
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                EdgeKind = edge.EdgeKind,
                IsWeak = edge.IsWeak
            });
        }

        var roots = new List<RootGroupData>(original.Roots.Count);
        foreach (var root in original.Roots)
        {
            roots.Add(new RootGroupData
            {
                RootKind = root.RootKind,
                RootCount = root.RootCount,
                ReachableBytes = root.ReachableBytes,
                RetainedBytes = root.RetainedBytes
            });
        }

        return new HeapGraphData
        {
            Nodes = nodes,
            Edges = edges,
            Roots = roots
        };
    }
}
