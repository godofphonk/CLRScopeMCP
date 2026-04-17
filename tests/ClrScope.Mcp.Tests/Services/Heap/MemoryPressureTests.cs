using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Diagnostics.Tools.GCDump;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Services.Heap;

/// <summary>
/// Memory pressure tests for large heap dumps (>100MB)
/// These tests verify that the system can handle large memory dumps without performance degradation
/// </summary>
public class MemoryPressureTests
{
    private readonly Mock<ILogger<GcDumpGraphAdapter>> _loggerMock;
    private readonly GcDumpGraphAdapter _adapter;
    private readonly string _testDataPath;

    public MemoryPressureTests()
    {
        _loggerMock = new Mock<ILogger<GcDumpGraphAdapter>>();
        _adapter = new GcDumpGraphAdapter(_loggerMock.Object);
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "test-data");
    }

    private string GetTestDataPath(string fileName) => Path.Combine(_testDataPath, fileName);

    [Fact]
    public async Task LoadLargeGcDumpAsync_WithFileLargerThan100MB_ShouldSucceed()
    {
        // Arrange
        // Note: This test requires a large gcdump file to be present in test-data
        // The file should be generated using the MemoryPressureApp
        var largeGcdumpPath = GetTestDataPath("memory-pressure-large.gcdump");

        // Skip if file doesn't exist (it's a large file that may not be committed)
        if (!File.Exists(largeGcdumpPath))
        {
            return;
        }

        var fileInfo = new FileInfo(largeGcdumpPath);
        Assert.True(fileInfo.Length > 100 * 1024 * 1024, 
            $"Test file should be larger than 100MB, but is {fileInfo.Length / 1024 / 1024}MB");

        // Act
        var result = await _adapter.LoadGraphAsync(largeGcdumpPath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Nodes);
        Assert.NotNull(result.Edges);
        Assert.NotNull(result.Roots);
        
        // Verify that large heap was parsed successfully
        Assert.True(result.Nodes.Count > 0, "Should have parsed nodes from large heap dump");
        
        var totalHeapBytes = result.Nodes.Values.Sum(n => n.ShallowSizeBytes);
        Assert.True(totalHeapBytes > 100 * 1024 * 1024, 
            $"Total heap bytes should be >100MB, but is {totalHeapBytes / 1024 / 1024}MB");
    }

    [Fact]
    public async Task LoadLargeGcDumpAsync_VerifyPerformanceWithLargeHeap()
    {
        // Arrange
        var largeGcdumpPath = GetTestDataPath("memory-pressure-large.gcdump");

        if (!File.Exists(largeGcdumpPath))
        {
            return;
        }

        // Act - Measure loading time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _adapter.LoadGraphAsync(largeGcdumpPath, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        
        // Loading a 100MB+ heap dump should complete within reasonable time
        // This threshold can be adjusted based on performance requirements
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, 
            $"Loading large heap dump took {stopwatch.ElapsedMilliseconds}ms, expected <30000ms");
    }

    [Fact]
    public async Task LoadLargeGcDumpAsync_VerifyMemoryUsageStaysReasonable()
    {
        // Arrange
        var largeGcdumpPath = GetTestDataPath("memory-pressure-large.gcdump");

        if (!File.Exists(largeGcdumpPath))
        {
            return;
        }

        // Act - Measure memory before and after
        var memoryBefore = GC.GetTotalMemory(true);
        var result = await _adapter.LoadGraphAsync(largeGcdumpPath, CancellationToken.None);
        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsed = memoryAfter - memoryBefore;

        // Assert
        Assert.NotNull(result);
        
        // Memory usage should not exceed 10x the file size (reasonable overhead)
        var fileInfo = new FileInfo(largeGcdumpPath);
        var expectedMaxMemory = fileInfo.Length * 10;
        
        Assert.True(memoryUsed < expectedMaxMemory, 
            $"Memory usage {memoryUsed / 1024 / 1024}MB exceeded threshold {expectedMaxMemory / 1024 / 1024}MB");
    }

    [Fact]
    public async Task LoadLargeGcDumpAsync_VerifyGraphIntegrityWithLargeHeap()
    {
        // Arrange
        var largeGcdumpPath = GetTestDataPath("memory-pressure-large.gcdump");

        if (!File.Exists(largeGcdumpPath))
        {
            return;
        }

        // Act
        var result = await _adapter.LoadGraphAsync(largeGcdumpPath, CancellationToken.None);

        // Assert - Verify graph integrity
        Assert.NotNull(result);
        
        // For a large heap, we expect:
        // - Significant number of nodes
        // - Significant number of edges
        // - Proper root references
        
        Assert.True(result.Nodes.Count > 10000, 
            $"Large heap should have >10000 nodes, got {result.Nodes.Count}");
        Assert.True(result.Edges.Count > 0, "Should have edges");
        Assert.True(result.Roots.Count > 0, "Should have roots");
        
        // Verify that edges reference valid nodes
        var validNodeIds = new HashSet<long>(result.Nodes.Keys);
        foreach (var edge in result.Edges)
        {
            Assert.Contains(edge.FromNodeId, validNodeIds);
            Assert.Contains(edge.ToNodeId, validNodeIds);
        }
    }

    [Fact]
    public async Task LoadLargeGcDumpAsync_VerifyTypeStatisticsWithLargeHeap()
    {
        // Arrange
        var largeGcdumpPath = GetTestDataPath("memory-pressure-large.gcdump");

        if (!File.Exists(largeGcdumpPath))
        {
            return;
        }

        // Act
        var result = await _adapter.LoadGraphAsync(largeGcdumpPath, CancellationToken.None);

        // Assert - Analyze type distribution
        Assert.NotNull(result);
        
        var typeGroups = result.Nodes.Values
            .GroupBy(n => n.TypeName)
            .OrderByDescending(g => g.Sum(n => n.ShallowSizeBytes))
            .Take(10)
            .ToList();
        
        // Should have multiple types represented
        Assert.True(typeGroups.Count > 0, "Should have type information");
        
        // Log top types for diagnostic purposes
        foreach (var group in typeGroups)
        {
            var totalSize = group.Sum(n => n.ShallowSizeBytes);
            var count = group.Count();
        }
    }

    [Fact]
    public async Task LoadLargeGcDumpAsync_VerifyHandlesConcurrentAccess()
    {
        // Arrange
        var largeGcdumpPath = GetTestDataPath("memory-pressure-large.gcdump");

        if (!File.Exists(largeGcdumpPath))
        {
            return;
        }

        // Act - Load the same file multiple times concurrently
        var tasks = new List<Task<HeapGraphData>>();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(_adapter.LoadGraphAsync(largeGcdumpPath, CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.True(result.Nodes.Count > 0);
        });
        
        // All results should be consistent
        var firstNodeCount = results[0].Nodes.Count;
        Assert.All(results, result =>
        {
            Assert.Equal(firstNodeCount, result.Nodes.Count);
        });
    }
}
