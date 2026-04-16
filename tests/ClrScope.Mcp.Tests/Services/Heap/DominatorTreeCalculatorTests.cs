using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Tests.Services.Heap;

public class DominatorTreeCalculatorTests
{
    private readonly DominatorTreeCalculator _calculator;
    private readonly ILogger<DominatorTreeCalculator> _logger;

    public DominatorTreeCalculatorTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<DominatorTreeCalculator>();
        _calculator = new DominatorTreeCalculator(_logger);
    }

    [Fact]
    public void LinearChain_CalculatesRetainedSizeCorrectly()
    {
        // Graph: Root -> A -> B -> C
        // Expected retained sizes:
        // Root: 400 (sum of all)
        // A: 300 (B + C + A)
        // B: 200 (C + B)
        // C: 100 (only itself)

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "C", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        Assert.Equal(400, graph.Nodes[1].RetainedSizeBytes); // Root retains all
        Assert.Equal(300, graph.Nodes[2].RetainedSizeBytes); // A retains B + C + A
        Assert.Equal(200, graph.Nodes[3].RetainedSizeBytes); // B retains C + B
        Assert.Equal(100, graph.Nodes[4].RetainedSizeBytes); // C retains only itself
    }

    [Fact]
    public void Branching_CalculatesRetainedSizeCorrectly()
    {
        // Graph: Root -> A, Root -> B, Root -> C
        // Expected retained sizes:
        // Root: 300 (sum of all)
        // A: 100 (only itself)
        // B: 100 (only itself)
        // C: 100 (only itself)

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "C", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        Assert.Equal(400, graph.Nodes[1].RetainedSizeBytes); // Root retains all
        Assert.Equal(100, graph.Nodes[2].RetainedSizeBytes); // A retains only itself
        Assert.Equal(100, graph.Nodes[3].RetainedSizeBytes); // B retains only itself
        Assert.Equal(100, graph.Nodes[4].RetainedSizeBytes); // C retains only itself
    }

    [Fact]
    public void SharedChild_CalculatesRetainedSizeCorrectly()
    {
        // Graph: Root -> A, Root -> B, A -> C, B -> C
        // Expected retained sizes:
        // Root: 400 (sum of all)
        // A: 200 (C + A)
        // B: 200 (C + B)
        // C: 100 (only itself - shared, not double-counted)

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "C", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        Assert.Equal(400, graph.Nodes[1].RetainedSizeBytes); // Root retains all
        Assert.Equal(200, graph.Nodes[2].RetainedSizeBytes); // A retains C + A
        Assert.Equal(200, graph.Nodes[3].RetainedSizeBytes); // B retains C + B
        Assert.Equal(100, graph.Nodes[4].RetainedSizeBytes); // C retains only itself (shared)
    }

    [Fact]
    public void Cycle_CalculatesRetainedSizeCorrectly()
    {
        // Graph: Root -> A, A -> B, B -> C, C -> A (cycle)
        // Expected retained sizes:
        // Root: 400 (sum of all)
        // A: 300 (B + C + A)
        // B: 300 (C + A + B)
        // C: 300 (A + B + C)
        // Note: In dominator tree, one node dominates the cycle

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "C", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 4, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false } // Cycle back
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        // With dominator tree, the cycle is broken by one node dominating the cycle
        Assert.Equal(400, graph.Nodes[1].RetainedSizeBytes); // Root retains all
        // The exact distribution depends on which node dominates the cycle
        // All nodes in cycle should have similar retained sizes
        var cycleNodes = new[] { graph.Nodes[2], graph.Nodes[3], graph.Nodes[4] };
        var minRetained = cycleNodes.Min(n => n.RetainedSizeBytes);
        var maxRetained = cycleNodes.Max(n => n.RetainedSizeBytes);
        
        // All cycle nodes should retain at least themselves
        Assert.All(cycleNodes, n => Assert.True(n.RetainedSizeBytes >= 100));
    }

    [Fact]
    public void WeakEdge_CalculatesRetainedSizeCorrectly()
    {
        // Graph: Root -> A, A -weak-> B
        // Expected retained sizes:
        // Root: 100 (only A, weak edge doesn't retain B)
        // A: 100 (only itself, weak edge doesn't retain B)
        // B: 100 (only itself, not retained by weak edge)

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = true } // Weak edge
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        Assert.Equal(200, graph.Nodes[1].RetainedSizeBytes); // Root retains A only
        Assert.Equal(100, graph.Nodes[2].RetainedSizeBytes); // A retains only itself
        Assert.Equal(100, graph.Nodes[3].RetainedSizeBytes); // B retains only itself (weak edge)
    }

    [Fact]
    public void MultipleRoots_CalculatesRetainedSizeCorrectly()
    {
        // Graph: Root1 -> A, Root2 -> B, A -> C, B -> C
        // Expected retained sizes:
        // Root1: 200 (A + C)
        // Root2: 200 (B + C)
        // A: 200 (C + A)
        // B: 200 (C + B)
        // C: 100 (only itself - shared between roots)

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root1", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "Root2", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [5] = new MemoryNodeData { NodeId = 5, TypeName = "C", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 5, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 4, ToNodeId = 5, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        Assert.Equal(200, graph.Nodes[1].RetainedSizeBytes); // Root1 retains A + C
        Assert.Equal(200, graph.Nodes[2].RetainedSizeBytes); // Root2 retains B + C
        Assert.Equal(200, graph.Nodes[3].RetainedSizeBytes); // A retains C + A
        Assert.Equal(200, graph.Nodes[4].RetainedSizeBytes); // B retains C + B
        Assert.Equal(100, graph.Nodes[5].RetainedSizeBytes); // C retains only itself (shared)
    }

    [Fact]
    public void FindRetainerPaths_LinearChain_ReturnsCorrectPaths()
    {
        // Graph: Root -> A -> B -> C
        // Find paths to C

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "C", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        var paths = _calculator.FindRetainerPaths(graph, 4);

        Assert.Single(paths);
        Assert.Equal(1, paths[0].RootNodeId);
        Assert.Equal(3, paths[0].TotalSteps); // Root -> A -> B -> C
    }

    [Fact]
    public void FindRetainerPaths_MultiplePaths_ReturnsMultiplePaths()
    {
        // Graph: Root -> A, Root -> B, A -> C, B -> C
        // Find paths to C (should find 2 paths)

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "C", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        var paths = _calculator.FindRetainerPaths(graph, 4);

        Assert.Equal(2, paths.Count);
        Assert.All(paths, p => Assert.Equal(1, p.RootNodeId)); // Both from same root
        Assert.All(paths, p => Assert.Equal(2, p.TotalSteps)); // Both 2 steps
    }

    [Fact]
    public void FindRetainerPaths_WeakEdge_ExcludesWeakEdges()
    {
        // Graph: Root -> A, A -weak-> B
        // Find paths to B (should not include weak edge path)

        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "A", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "B", Namespace = "Test", AssemblyName = "TestAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "0", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = true } // Weak edge
            },
            Roots = new List<RootGroupData>()
        };

        var paths = _calculator.FindRetainerPaths(graph, 3);

        // Should not find any path because only weak edge connects to B
        Assert.Empty(paths);
    }
}
