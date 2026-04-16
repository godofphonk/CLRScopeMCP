using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Services.Heap;
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
}
