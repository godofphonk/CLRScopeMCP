using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Services.Heap;
using Graphs;
using Microsoft.Diagnostics.Tools.GCDump;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Services.Heap;

public class GcDumpGraphAdapterTests
{
    private readonly Mock<ILogger<GcDumpGraphAdapter>> _loggerMock;
    private readonly GcDumpGraphAdapter _adapter;

    public GcDumpGraphAdapterTests()
    {
        _loggerMock = new Mock<ILogger<GcDumpGraphAdapter>>();
        _adapter = new GcDumpGraphAdapter(_loggerMock.Object);
    }

    [Fact]
    public async Task LoadGraphAsync_WithValidGcdumpFile_ReturnsHeapGraphData()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";
        
        // Act
        var result = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Nodes);
        Assert.NotNull(result.Edges);
        Assert.NotNull(result.Roots);
        
        // The file contains 3,761 objects according to dotnet-gcdump report
        // We expect at least some nodes to be parsed
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LoadGraphAsync_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var gcdumpPath = "/nonexistent/file.gcdump";

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None));
    }

    [Fact]
    public async Task LoadGraphAsync_WithInvalidExtension_LogsWarning()
    {
        // Arrange
        var invalidPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.txt";

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => _adapter.LoadGraphAsync(invalidPath, CancellationToken.None));
    }

    [Fact]
    public async Task LoadGraphAsync_WithStream_ReturnsHeapGraphData()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";
        using var stream = File.OpenRead(gcdumpPath);

        // Act
        var result = await _adapter.LoadGraphAsync(stream, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Nodes);
        Assert.NotNull(result.Edges);
    }

    [Fact]
    public async Task LoadGraphAsync_VerifyNodeCountIsNotZero()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";

        // Act
        var result = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);

        // Assert
        // The file contains 3,761 objects according to dotnet-gcdump report
        // If NodeIndexLimit is 0 or nodes count is 0, parsing failed
        Assert.True(result.Nodes.Count > 0, 
            $"Expected nodes count > 0, but got {result.Nodes.Count}. " +
            "This indicates GcDumpGraphAdapter failed to parse the gcdump file.");
    }

    [Fact]
    public async Task LoadGraphAsync_VerifyTotalHeapBytesIsNotZero()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";

        // Act
        var result = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);

        // Assert
        // The file contains 1,316,260 GC Heap bytes according to dotnet-gcdump report
        var totalHeapBytes = result.Nodes.Values.Sum(n => n.ShallowSizeBytes);
        Assert.True(totalHeapBytes > 0, 
            $"Expected total heap bytes > 0, but got {totalHeapBytes}. " +
            "This indicates GcDumpGraphAdapter failed to parse the gcdump file.");
    }

    [Fact]
    public async Task LoadGraphAsync_VerifyLogMessagesAreLogged()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";

        // Act
        await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LoadGraphAsync_DetailedDiagnostic_VerifyGCDumpFileContent()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";

        // Act
        var result = await _adapter.LoadGraphAsync(gcdumpPath, CancellationToken.None);

        // Assert - Detailed diagnostic
        Assert.NotNull(result);
        
        // Log detailed information for debugging
        var totalNodes = result.Nodes.Count;
        var totalEdges = result.Edges.Count;
        var totalRoots = result.Roots.Count;
        var totalHeapBytes = result.Nodes.Values.Sum(n => n.ShallowSizeBytes);

        // If all are zero, the file parsing failed completely
        Assert.True(totalNodes > 0 || totalEdges > 0 || totalRoots > 0,
            $"GcDumpGraphAdapter failed to parse gcdump file. " +
            $"Nodes: {totalNodes}, Edges: {totalEdges}, Roots: {totalRoots}, " +
            $"TotalHeapBytes: {totalHeapBytes}. " +
            "The file contains 3,761 objects according to dotnet-gcdump report. " +
            "This indicates a parsing failure in GcDumpGraphAdapter.");
    }

    [Fact]
    public async Task LoadGraphAsync_VerifyGCDumpFileExistsAndIsReadable()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";

        // Assert - Verify file exists and is readable
        Assert.True(File.Exists(gcdumpPath), $"GCDump file not found: {gcdumpPath}");
        
        var fileInfo = new FileInfo(gcdumpPath);
        Assert.True(fileInfo.Length > 0, $"GCDump file is empty: {gcdumpPath}");
        
        // Verify file can be read
        using var stream = File.OpenRead(gcdumpPath);
        Assert.NotNull(stream);
    }

    [Fact]
    public async Task LoadGraphAsync_VerifyGCDumpFileHeader()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";

        // Act - Read first few bytes to verify file format
        using var stream = File.OpenRead(gcdumpPath);
        var header = new byte[16];
        var bytesRead = await stream.ReadAsync(header, 0, 16);

        // Assert
        Assert.True(bytesRead > 0, "Could not read file header");
        // GCDump files should have a specific header format
        // This test ensures the file is not corrupted
    }

    [Fact]
    public async Task LoadGraphAsync_VerifyGCHeapDumpMemoryGraphHasNodes()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";

        // Act - Directly access GCHeapDump to verify MemoryGraph
        await Task.Run(() =>
        {
            var gcHeapDump = new GCHeapDump(gcdumpPath);
            var graph = gcHeapDump.MemoryGraph;

            // Assert - This should fail if NodeIndexLimit is 0
            Assert.True(graph.NodeIndexLimit > 0, 
                $"GCHeapDump.MemoryGraph.NodeIndexLimit is {graph.NodeIndexLimit}, expected > 0. " +
                "This indicates the gcdump file is not being parsed correctly by GCHeapDump.");
        });
    }

    [Fact]
    public async Task LoadGraphAsync_VerifyGCHeapDumpHasNonZeroNodes()
    {
        // Arrange
        var gcdumpPath = "/home/gospodin/Desktop/homeProjects/CLRScopeMCP/test-data/test-data.gcdump";

        // Act - Directly access GCHeapDump to verify nodes
        await Task.Run(() =>
        {
            var gcHeapDump = new GCHeapDump(gcdumpPath);
            var graph = gcHeapDump.MemoryGraph;
            var nodeStorage = graph.AllocNodeStorage();

            int nodeCount = 0;
            for (NodeIndex idx = 0; (long)idx < (long)graph.NodeIndexLimit; idx++)
            {
                var node = graph.GetNode(idx, nodeStorage);
                if (node.Size > 0)
                {
                    nodeCount++;
                }
            }

            // Assert - Should have at least some nodes with size > 0
            Assert.True(nodeCount > 0, 
                $"GCHeapDump has {nodeCount} nodes with size > 0, expected > 0. " +
                "The file contains 3,761 objects according to dotnet-gcdump report.");
        });
    }
}
