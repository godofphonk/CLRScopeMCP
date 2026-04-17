using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.Logging;

namespace ClrScope.Benchmarks.HeapAnalysis;

/// <summary>
/// Benchmarks for heap-analysis operations.
/// Run with: dotnet run -c Release --project bench/ClrScope.Benchmarks/ClrScope.Benchmarks.csproj
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net60)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[Config(typeof(Config))]
public class HeapAnalysisBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            SummaryStyle = BenchmarkDotNet.Reports.SummaryStyle.Default
                .WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Percentage);
        }
    }

    private GcDumpProcessAdapter _adapter = null!;
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

        var adapterLogger = loggerFactory.CreateLogger<GcDumpProcessAdapter>();
        var calculatorLogger = loggerFactory.CreateLogger<DominatorTreeCalculator>();

        _adapter = new GcDumpProcessAdapter(adapterLogger);
        _calculator = new DominatorTreeCalculator(calculatorLogger);

        _gcdumpPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
        
        if (!File.Exists(_gcdumpPath))
        {
            throw new FileNotFoundException($"Test data file not found: {_gcdumpPath}");
        }

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
    [BenchmarkCategory("Load")]
    public async Task LoadGcDump()
    {
        var graph = await _adapter.LoadGraphAsync(_gcdumpPath, CancellationToken.None);
        graph = null;
    }

    [Benchmark]
    [BenchmarkCategory("Analysis")]
    public void CalculateRetainedSize()
    {
        var graphCopy = CloneGraph(_graph);
        _calculator.CalculateRetainedSize(graphCopy);
    }

    [Benchmark]
    [BenchmarkCategory("Analysis")]
    public void FindRetainerPaths()
    {
        var graphCopy = CloneGraph(_graph);
        _calculator.CalculateRetainedSize(graphCopy);
        
        var targetNodeId = graphCopy.Nodes.Values.FirstOrDefault(n => !n.IsRoot)?.NodeId 
            ?? graphCopy.Nodes.Keys.First();
        
        _calculator.FindRetainerPaths(graphCopy, targetNodeId, maxPaths: 10);
    }

    [Benchmark]
    [BenchmarkCategory("Analysis")]
    public void GetTopTypesByRetainedSize()
    {
        var graphCopy = CloneGraph(_graph);
        _calculator.CalculateRetainedSize(graphCopy);
        
        var topTypes = graphCopy.Nodes.Values
            .GroupBy(n => n.TypeName)
            .Select(g => new
            {
                TypeName = g.Key,
                RetainedSize = g.Sum(n => n.RetainedSizeBytes),
                Count = g.Sum(n => n.Count)
            })
            .OrderByDescending(t => t.RetainedSize)
            .Take(50)
            .ToArray();
    }

    [Benchmark]
    [BenchmarkCategory("Analysis")]
    public void GetTopObjectsByRetainedSize()
    {
        var graphCopy = CloneGraph(_graph);
        _calculator.CalculateRetainedSize(graphCopy);
        
        var topObjects = graphCopy.Nodes.Values
            .Where(n => !n.IsRoot)
            .OrderByDescending(n => n.RetainedSizeBytes)
            .Take(100)
            .ToArray();
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

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public void CloneGraphMemory()
    {
        var graphCopy = CloneGraph(_graph);
        graphCopy = null;
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
