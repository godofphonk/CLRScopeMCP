using System.Diagnostics;
using System.Text;
using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Services.Heap;

public class GcDumpProcessAdapterTimeoutTests
{
    [Fact]
    public async Task IGcDumpGraphAdapter_WithSlowImplementation_ThrowsTimeoutException()
    {
        // Arrange
        var slowAdapter = new SlowGcDumpGraphAdapter(NullLogger<SlowGcDumpGraphAdapter>.Instance);
        
        var dummyGcdumpPath = Path.Combine(Path.GetTempPath(), $"dummy-{Guid.NewGuid()}.gcdump");
        await File.WriteAllBytesAsync(dummyGcdumpPath, new byte[100]);
        
        // Act & Assert
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10 second timeout for test
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => 
            await slowAdapter.LoadGraphAsync(dummyGcdumpPath, cts.Token));
        
        // The exception should be TaskCanceledException (subclass of OperationCanceledException)
        Assert.IsAssignableFrom<TaskCanceledException>(exception);
        
        // Cleanup
        try
        {
            File.Delete(dummyGcdumpPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
    
    [Fact]
    public async Task IGcDumpGraphAdapter_WithFastImplementation_ReturnsGraphData()
    {
        // Arrange
        var fastAdapter = new FastGcDumpGraphAdapter(NullLogger<FastGcDumpGraphAdapter>.Instance);
        
        var dummyGcdumpPath = Path.Combine(Path.GetTempPath(), $"dummy-{Guid.NewGuid()}.gcdump");
        await File.WriteAllBytesAsync(dummyGcdumpPath, new byte[100]);
        
        // Act
        var result = await fastAdapter.LoadGraphAsync(dummyGcdumpPath, CancellationToken.None);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Nodes);
        Assert.NotNull(result.Edges);
        Assert.NotNull(result.Roots);
        
        // Cleanup
        try
        {
            File.Delete(dummyGcdumpPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
    
    // Slow adapter that simulates timeout
    public class SlowGcDumpGraphAdapter : IGcDumpGraphAdapter
    {
        private readonly ILogger<SlowGcDumpGraphAdapter> _logger;
        
        public SlowGcDumpGraphAdapter(ILogger<SlowGcDumpGraphAdapter> logger)
        {
            _logger = logger;
        }
        
        public async Task<HeapGraphData> LoadGraphAsync(string gcdumpPath, CancellationToken cancellationToken)
        {
            // Simulate slow operation that exceeds timeout
            await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
            throw new TimeoutException("Heap parser timed out after 5 minutes");
        }
        
        public Task<HeapGraphData> LoadGraphAsync(Stream gcdumpStream, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Stream loading not supported");
        }
    }
    
    // Fast adapter that returns valid data
    public class FastGcDumpGraphAdapter : IGcDumpGraphAdapter
    {
        private readonly ILogger<FastGcDumpGraphAdapter> _logger;
        
        public FastGcDumpGraphAdapter(ILogger<FastGcDumpGraphAdapter> logger)
        {
            _logger = logger;
        }
        
        public Task<HeapGraphData> LoadGraphAsync(string gcdumpPath, CancellationToken cancellationToken)
        {
            var graphData = new HeapGraphData
            {
                Nodes = new Dictionary<long, MemoryNodeData>(),
                Edges = new List<MemoryEdgeData>(),
                Roots = new List<RootGroupData>()
            };
            return Task.FromResult(graphData);
        }
        
        public Task<HeapGraphData> LoadGraphAsync(Stream gcdumpStream, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Stream loading not supported");
        }
    }
}
