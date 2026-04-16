using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Tests.Services.Heap;

/// <summary>
/// Integration tests for DominatorTreeCalculator with realistic memory leak scenarios.
/// </summary>
public class DominatorTreeCalculatorIntegrationTests
{
    private readonly DominatorTreeCalculator _calculator;
    private readonly ILogger<DominatorTreeCalculator> _logger;

    public DominatorTreeCalculatorIntegrationTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<DominatorTreeCalculator>();
        _calculator = new DominatorTreeCalculator(_logger);
    }

    [Fact]
    public void StaticFieldLeak_CalculatesRetainedSizeCorrectly()
    {
        // Scenario: Static field holds reference to large object
        // Graph structure:
        // Root (Static) -> StaticField -> LargeObject -> Data[0..N]
        // Expected: StaticField retains entire LargeObject and its data
        
        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "System", AssemblyName = "mscorlib", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "StaticFieldHolder", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "LargeObject", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 1000, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "DataItem[]", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 500, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [5] = new MemoryNodeData { NodeId = 5, TypeName = "DataItem", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [6] = new MemoryNodeData { NodeId = 6, TypeName = "DataItem", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Static", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "ArrayElement", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 4, ToNodeId = 5, EdgeKind = "ArrayElement", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 4, ToNodeId = 6, EdgeKind = "ArrayElement", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        // Static root should retain everything (1700 bytes total)
        Assert.Equal(1900, graph.Nodes[1].RetainedSizeBytes);
        // StaticField retains LargeObject + Data (1800 bytes)
        Assert.Equal(1800, graph.Nodes[2].RetainedSizeBytes);
        // LargeObject retains Data array + itself (1500 bytes)
        Assert.Equal(1700, graph.Nodes[3].RetainedSizeBytes);
        // Data array retains items + itself (700 bytes)
        Assert.Equal(700, graph.Nodes[4].RetainedSizeBytes);
        // Items retain only themselves
        Assert.Equal(100, graph.Nodes[5].RetainedSizeBytes);
        Assert.Equal(100, graph.Nodes[6].RetainedSizeBytes);
    }

    [Fact]
    public void EventSubscriptionLeak_CalculatesRetainedSizeCorrectly()
    {
        // Scenario: Event handler not unsubscribed causes leak
        // Graph structure:
        // Root (Static) -> EventPublisher -> EventHandler -> LeakedObject
        // Expected: EventPublisher retains EventHandler which retains LeakedObject
        
        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "System", AssemblyName = "mscorlib", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "EventPublisher", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "EventHandler", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "LeakedObject", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 1000, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [5] = new MemoryNodeData { NodeId = 5, TypeName = "Data", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 500, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Static", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Event", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 4, ToNodeId = 5, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        // Static root retains entire chain (1700 bytes)
        Assert.Equal(1700, graph.Nodes[1].RetainedSizeBytes);
        // EventPublisher retains everything through event (1600 bytes)
        Assert.Equal(1600, graph.Nodes[2].RetainedSizeBytes);
        // EventHandler retains LeakedObject + Data (1500 bytes)
        Assert.Equal(1500, graph.Nodes[3].RetainedSizeBytes);
        // LeakedObject retains Data + itself (1500 bytes)
        Assert.Equal(1500, graph.Nodes[4].RetainedSizeBytes);
        // Data retains only itself
        Assert.Equal(500, graph.Nodes[5].RetainedSizeBytes);
    }

    [Fact]
    public void WeakReferenceCache_CalculatesRetainedSizeCorrectly()
    {
        // Scenario: WeakReference should not retain objects
        // Graph structure:
        // Root -> Cache -> WeakReference -> CachedObject (weak edge)
        // Expected: CachedObject should NOT be retained by WeakReference
        
        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "System", AssemblyName = "mscorlib", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "Cache", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "WeakReference", Namespace = "System", AssemblyName = "mscorlib", ShallowSizeBytes = 50, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "CachedObject", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 1000, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [5] = new MemoryNodeData { NodeId = 5, TypeName = "Data", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 500, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Static", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "WeakReference", IsWeak = true }, // Weak edge
                new MemoryEdgeData { FromNodeId = 4, ToNodeId = 5, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        _calculator.CalculateRetainedSize(graph);

        // Static root retains Cache + WeakReference (250 bytes)
        Assert.Equal(250, graph.Nodes[1].RetainedSizeBytes);
        // Cache retains WeakReference (150 bytes)
        Assert.Equal(150, graph.Nodes[2].RetainedSizeBytes);
        // WeakReference retains only itself (50 bytes) - weak edge doesn't retain
        Assert.Equal(50, graph.Nodes[3].RetainedSizeBytes);
        // CachedObject retains Data + itself (1500 bytes) - not retained by weak edge
        Assert.Equal(1500, graph.Nodes[4].RetainedSizeBytes);
        // Data retains only itself
        Assert.Equal(500, graph.Nodes[5].RetainedSizeBytes);
    }

    [Fact]
    public void FindRetainerPaths_StaticFieldLeak_ReturnsCorrectPath()
    {
        // Scenario: Find retainer path for leaked object in static field leak
        
        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "System", AssemblyName = "mscorlib", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "StaticFieldHolder", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "LeakedObject", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 1000, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Static", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        var paths = _calculator.FindRetainerPaths(graph, 3);

        Assert.Single(paths);
        Assert.Equal(1, paths[0].RootNodeId);
        Assert.Equal("Static", paths[0].RootKind);
        Assert.Equal(2, paths[0].TotalSteps);
        Assert.Equal("Static", paths[0].Steps[0].EdgeKind);
    }

    [Fact]
    public void FindRetainerPaths_EventSubscriptionLeak_ReturnsCorrectPath()
    {
        // Scenario: Find retainer path for leaked object in event subscription leak
        
        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "System", AssemblyName = "mscorlib", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "EventPublisher", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "EventHandler", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "LeakedObject", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 1000, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Static", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Event", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "Reference", IsWeak = false }
            },
            Roots = new List<RootGroupData>()
        };

        var paths = _calculator.FindRetainerPaths(graph, 4);

        Assert.Single(paths);
        Assert.Equal(1, paths[0].RootNodeId);
        Assert.Equal(3, paths[0].TotalSteps);
        Assert.Equal("Event", paths[0].Steps[1].EdgeKind);
    }

    [Fact]
    public void FindRetainerPaths_WeakReferenceCache_NoPathFound()
    {
        // Scenario: WeakReference should not provide retainer path
        
        var graph = new HeapGraphData
        {
            Nodes = new Dictionary<long, MemoryNodeData>
            {
                [1] = new MemoryNodeData { NodeId = 1, TypeName = "Root", Namespace = "System", AssemblyName = "mscorlib", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = true, RootKind = "Static" },
                [2] = new MemoryNodeData { NodeId = 2, TypeName = "Cache", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 100, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [3] = new MemoryNodeData { NodeId = 3, TypeName = "WeakReference", Namespace = "System", AssemblyName = "mscorlib", ShallowSizeBytes = 50, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false },
                [4] = new MemoryNodeData { NodeId = 4, TypeName = "CachedObject", Namespace = "MyApp", AssemblyName = "MyAssembly", ShallowSizeBytes = 1000, RetainedSizeBytes = 0, Count = 1, Generation = "2", IsRoot = false }
            },
            Edges = new List<MemoryEdgeData>
            {
                new MemoryEdgeData { FromNodeId = 1, ToNodeId = 2, EdgeKind = "Static", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 2, ToNodeId = 3, EdgeKind = "Reference", IsWeak = false },
                new MemoryEdgeData { FromNodeId = 3, ToNodeId = 4, EdgeKind = "WeakReference", IsWeak = true } // Weak edge
            },
            Roots = new List<RootGroupData>()
        };

        var paths = _calculator.FindRetainerPaths(graph, 4);

        // Should not find any path because only weak edge connects to CachedObject
        Assert.Empty(paths);
    }
}
