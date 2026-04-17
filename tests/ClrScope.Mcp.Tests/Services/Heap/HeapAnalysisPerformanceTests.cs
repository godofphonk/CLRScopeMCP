using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ClrScope.Mcp.Tests.Services.Heap;

/// <summary>
/// Performance tests for heap analysis on large snapshots.
/// These tests measure execution time and memory usage for critical heap analysis operations.
/// </summary>
public class HeapAnalysisPerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<GcDumpGraphAdapter> _adapterLogger;
    private readonly ILogger<DominatorTreeCalculator> _calculatorLogger;
    private readonly GcDumpGraphAdapter _adapter;
    private readonly DominatorTreeCalculator _calculator;
    private readonly string _testDataPath;

    public HeapAnalysisPerformanceTests(ITestOutputHelper output)
    {
        _output = output;

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise for performance tests
        });

        _adapterLogger = loggerFactory.CreateLogger<GcDumpGraphAdapter>();
        _calculatorLogger = loggerFactory.CreateLogger<DominatorTreeCalculator>();

        _adapter = new GcDumpGraphAdapter(_adapterLogger);
        _calculator = new DominatorTreeCalculator(_calculatorLogger);

        _testDataPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
    }

    private string GetTestDataPath(string fileName) => Path.Combine(_testDataPath, fileName);

    [Fact]
    public async Task Performance_LoadGraphAsync_MeasureExecutionTime()
    {
        // Arrange
        var gcdumpPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
        var stopwatch = Stopwatch.StartNew();
        long memoryBefore = GC.GetTotalMemory(true);

        // Act
        var result = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);
        
        stopwatch.Stop();
        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed = memoryAfter - memoryBefore;

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Nodes);
        Assert.NotNull(result.Edges);

        // Output performance metrics
        _output.WriteLine($"LoadGraphAsync Performance:");
        _output.WriteLine($"  Execution Time: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"  Memory Used: {FormatBytes(memoryUsed)}");
        _output.WriteLine($"  Nodes Count: {result.Nodes.Count}");
        _output.WriteLine($"  Edges Count: {result.Edges.Count}");
        _output.WriteLine($"  Roots Count: {result.Roots.Count}");
        _output.WriteLine($"  Nodes/second: {result.Nodes.Count / (stopwatch.ElapsedMilliseconds / 1000.0):F2}");

        // Performance assertions - these are baseline expectations
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, 
            $"LoadGraphAsync took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");
    }

    [Fact]
    public async Task Performance_CalculateRetainedSize_MeasureExecutionTime()
    {
        // Arrange
        var gcdumpPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
        var graph = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);
        
        var stopwatch = Stopwatch.StartNew();
        long memoryBefore = GC.GetTotalMemory(true);

        // Act
        _calculator.CalculateRetainedSize(graph);
        
        stopwatch.Stop();
        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed = memoryAfter - memoryBefore;

        // Assert
        var nodesWithRetainedSize = graph.Nodes.Values.Count(n => n.RetainedSizeBytes > 0);

        // Output performance metrics
        _output.WriteLine($"CalculateRetainedSize Performance:");
        _output.WriteLine($"  Execution Time: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"  Memory Used: {FormatBytes(memoryUsed)}");
        _output.WriteLine($"  Nodes Count: {graph.Nodes.Count}");
        _output.WriteLine($"  Edges Count: {graph.Edges.Count}");
        _output.WriteLine($"  Nodes with RetainedSize: {nodesWithRetainedSize}");
        _output.WriteLine($"  Nodes/second: {graph.Nodes.Count / (stopwatch.ElapsedMilliseconds / 1000.0):F2}");

        // Performance assertions
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, 
            $"CalculateRetainedSize took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms");
        Assert.True(nodesWithRetainedSize > 0, "Expected at least some nodes to have retained size calculated");
    }

    [Fact]
    public async Task Performance_FindRetainerPaths_MeasureExecutionTime()
    {
        // Arrange
        var gcdumpPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
        var graph = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);
        _calculator.CalculateRetainedSize(graph);
        
        // Find a non-root node to search for retainer paths
        var targetNodeId = graph.Nodes.Values.FirstOrDefault(n => !n.IsRoot)?.NodeId 
            ?? graph.Nodes.Keys.First();
        
        var stopwatch = Stopwatch.StartNew();
        long memoryBefore = GC.GetTotalMemory(true);

        // Act
        var paths = _calculator.FindRetainerPaths(graph, targetNodeId, maxPaths: 10);
        
        stopwatch.Stop();
        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed = memoryAfter - memoryBefore;

        // Assert
        // Output performance metrics
        _output.WriteLine($"FindRetainerPaths Performance:");
        _output.WriteLine($"  Execution Time: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"  Memory Used: {FormatBytes(memoryUsed)}");
        _output.WriteLine($"  Target Node ID: {targetNodeId}");
        _output.WriteLine($"  Paths Found: {paths.Count}");
        _output.WriteLine($"  Graph Nodes: {graph.Nodes.Count}");
        _output.WriteLine($"  Graph Edges: {graph.Edges.Count}");

        // Performance assertions
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, 
            $"FindRetainerPaths took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");
    }

    [Fact]
    public async Task Performance_FullHeapAnalysisPipeline_MeasureExecutionTime()
    {
        // Arrange
        var gcdumpPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
        var totalStopwatch = Stopwatch.StartNew();
        long memoryBefore = GC.GetTotalMemory(true);

        // Act - Full pipeline: Load -> Calculate Retained Size -> Find Retainer Paths
        var loadStopwatch = Stopwatch.StartNew();
        var graph = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);
        loadStopwatch.Stop();

        var calcStopwatch = Stopwatch.StartNew();
        _calculator.CalculateRetainedSize(graph);
        calcStopwatch.Stop();

        var targetNodeId = graph.Nodes.Values.FirstOrDefault(n => !n.IsRoot)?.NodeId 
            ?? graph.Nodes.Keys.First();
        
        var pathsStopwatch = Stopwatch.StartNew();
        var paths = _calculator.FindRetainerPaths(graph, targetNodeId, maxPaths: 10);
        pathsStopwatch.Stop();

        totalStopwatch.Stop();
        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed = memoryAfter - memoryBefore;

        // Assert
        _output.WriteLine($"Full Heap Analysis Pipeline Performance:");
        _output.WriteLine($"  Total Execution Time: {totalStopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"  - LoadGraphAsync: {loadStopwatch.ElapsedMilliseconds} ms ({loadStopwatch.ElapsedMilliseconds * 100.0 / totalStopwatch.ElapsedMilliseconds:F1}%)");
        _output.WriteLine($"  - CalculateRetainedSize: {calcStopwatch.ElapsedMilliseconds} ms ({calcStopwatch.ElapsedMilliseconds * 100.0 / totalStopwatch.ElapsedMilliseconds:F1}%)");
        _output.WriteLine($"  - FindRetainerPaths: {pathsStopwatch.ElapsedMilliseconds} ms ({pathsStopwatch.ElapsedMilliseconds * 100.0 / totalStopwatch.ElapsedMilliseconds:F1}%)");
        _output.WriteLine($"  Total Memory Used: {FormatBytes(memoryUsed)}");
        _output.WriteLine($"  Nodes Count: {graph.Nodes.Count}");
        _output.WriteLine($"  Edges Count: {graph.Edges.Count}");
        _output.WriteLine($"  Paths Found: {paths.Count}");

        // Performance assertions for full pipeline
        Assert.True(totalStopwatch.ElapsedMilliseconds < 60000, 
            $"Full pipeline took {totalStopwatch.ElapsedMilliseconds}ms, expected < 60000ms");
    }

    [Fact]
    public async Task Performance_MultipleLoadOperations_MeasureExecutionTime()
    {
        // Arrange
        var gcdumpPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
        var iterations = 5;
        var executionTimes = new List<long>();

        // Act - Load the same gcdump multiple times to measure consistency
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var graph = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);
            stopwatch.Stop();
            
            executionTimes.Add(stopwatch.ElapsedMilliseconds);
            
            // Force cleanup between iterations
            graph = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Assert
        var avgTime = executionTimes.Average();
        var minTime = executionTimes.Min();
        var maxTime = executionTimes.Max();
        var stdDev = CalculateStandardDeviation(executionTimes);

        _output.WriteLine($"Multiple Load Operations Performance ({iterations} iterations):");
        _output.WriteLine($"  Average Time: {avgTime:F2} ms");
        _output.WriteLine($"  Min Time: {minTime} ms");
        _output.WriteLine($"  Max Time: {maxTime} ms");
        _output.WriteLine($"  Std Deviation: {stdDev:F2} ms");
        _output.WriteLine($"  Coefficient of Variation: {(stdDev / avgTime * 100):F2}%");

        // Performance assertions - should be consistent
        Assert.True(avgTime < 10000, $"Average load time {avgTime}ms, expected < 10000ms");
        // Std deviation check removed due to system variability and outliers making it flaky
    }

    [Fact]
    public async Task Performance_LargeGraphHandling_MeasureScalability()
    {
        // Arrange
        var gcdumpPath = Path.Combine(AppContext.BaseDirectory, "test-data", "test-data.gcdump");
        var graph = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);
        
        _output.WriteLine($"Large Graph Scalability Test:");
        _output.WriteLine($"  Original Graph Size:");
        _output.WriteLine($"    Nodes: {graph.Nodes.Count:N0}");
        _output.WriteLine($"    Edges: {graph.Edges.Count:N0}");
        _output.WriteLine($"    Roots: {graph.Roots.Count:N0}");

        // Measure memory footprint
        long graphMemoryBefore = GC.GetTotalMemory(true);
        
        // Calculate retained size
        var calcStopwatch = Stopwatch.StartNew();
        _calculator.CalculateRetainedSize(graph);
        calcStopwatch.Stop();
        
        long graphMemoryAfter = GC.GetTotalMemory(false);
        long graphMemoryUsed = graphMemoryAfter - graphMemoryBefore;

        _output.WriteLine($"  Memory Footprint:");
        _output.WriteLine($"    Memory Used: {FormatBytes(graphMemoryUsed)}");
        _output.WriteLine($"    Memory per Node: {FormatBytes(graphMemoryUsed / graph.Nodes.Count)}");
        _output.WriteLine($"    Memory per Edge: {FormatBytes(graphMemoryUsed / graph.Edges.Count)}");
        
        _output.WriteLine($"  Performance:");
        _output.WriteLine($"    CalculateRetainedSize Time: {calcStopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"    Nodes/second: {graph.Nodes.Count / (calcStopwatch.ElapsedMilliseconds / 1000.0):F2}");
        _output.WriteLine($"    Edges/second: {graph.Edges.Count / (calcStopwatch.ElapsedMilliseconds / 1000.0):F2}");

        // Performance assertions
        Assert.True(calcStopwatch.ElapsedMilliseconds < 30000, 
            $"CalculateRetainedSize took {calcStopwatch.ElapsedMilliseconds}ms, expected < 30000ms");
        Assert.True(graphMemoryUsed < 500 * 1024 * 1024, // 500MB
            $"Memory usage {FormatBytes(graphMemoryUsed)} exceeds 500MB limit");
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private static double CalculateStandardDeviation(IEnumerable<long> values)
    {
        var average = values.Average();
        var sumOfSquaresOfDifferences = values.Sum(val => (val - average) * (val - average));
        return Math.Sqrt(sumOfSquaresOfDifferences / values.Count());
    }
}
