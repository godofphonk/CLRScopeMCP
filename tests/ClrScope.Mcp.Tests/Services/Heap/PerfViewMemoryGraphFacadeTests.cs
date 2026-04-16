using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Services.Heap;
using Graphs;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Services.Heap;

public class PerfViewMemoryGraphFacadeTests
{
    private readonly Mock<ILogger<PerfViewMemoryGraphFacade>> _loggerMock;
    private readonly PerfViewMemoryGraphFacade _facade;

    public PerfViewMemoryGraphFacadeTests()
    {
        _loggerMock = new Mock<ILogger<PerfViewMemoryGraphFacade>>();
        _facade = new PerfViewMemoryGraphFacade(_loggerMock.Object);
    }

    [Fact]
    public void GetNodes_WithValidMemoryGraph_ReturnsNodes()
    {
        // Arrange
        var memoryGraph = new MemoryGraph(100);

        // Act
        var nodes = _facade.GetNodes(memoryGraph);

        // Assert
        Assert.NotNull(nodes);
        // Empty graph returns empty list, which is valid
    }

    [Fact]
    public void GetNodes_WithNullMemoryGraph_ReturnsEmptyList()
    {
        // Arrange
        object? memoryGraph = null;

        // Act
        var nodes = _facade.GetNodes(memoryGraph!);

        // Assert
        Assert.NotNull(nodes);
        Assert.Empty(nodes);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetNodes_WithNonMemoryGraph_ReturnsEmptyList()
    {
        // Arrange
        object memoryGraph = new object();

        // Act
        var nodes = _facade.GetNodes(memoryGraph);

        // Assert
        Assert.NotNull(nodes);
        Assert.Empty(nodes);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetNodes_WithEmptyMemoryGraph_DoesNotThrow()
    {
        // Arrange
        var memoryGraph = new MemoryGraph(100);
        
        // Act
        var nodes = _facade.GetNodes(memoryGraph);

        // Assert
        Assert.NotNull(nodes);
        // Should not throw exception even with empty graph
    }

    [Fact]
    public void GetEdges_WithValidMemoryGraph_ReturnsEdges()
    {
        // Arrange
        var memoryGraph = new MemoryGraph(100);

        // Act
        var edges = _facade.GetEdges(memoryGraph);

        // Assert
        Assert.NotNull(edges);
    }

    [Fact]
    public void GetEdges_WithNullMemoryGraph_ReturnsEmptyList()
    {
        // Arrange
        object? memoryGraph = null;

        // Act
        var edges = _facade.GetEdges(memoryGraph!);

        // Assert
        Assert.NotNull(edges);
        Assert.Empty(edges);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetEdges_WithNonMemoryGraph_ReturnsEmptyList()
    {
        // Arrange
        object memoryGraph = new object();

        // Act
        var edges = _facade.GetEdges(memoryGraph);

        // Assert
        Assert.NotNull(edges);
        Assert.Empty(edges);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetRoots_WithValidMemoryGraph_ReturnsRoots()
    {
        // Arrange
        var memoryGraph = new MemoryGraph(100);

        // Act
        var roots = _facade.GetRoots(memoryGraph);

        // Assert
        Assert.NotNull(roots);
    }

    [Fact]
    public void GetRoots_WithNullMemoryGraph_ReturnsEmptyList()
    {
        // Arrange
        object? memoryGraph = null;

        // Act
        var roots = _facade.GetRoots(memoryGraph!);

        // Assert
        Assert.NotNull(roots);
        Assert.Empty(roots);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetRoots_WithNonMemoryGraph_ReturnsEmptyList()
    {
        // Arrange
        object memoryGraph = new object();

        // Act
        var roots = _facade.GetRoots(memoryGraph);

        // Assert
        Assert.NotNull(roots);
        Assert.Empty(roots);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetRoots_WithEmptyMemoryGraph_DoesNotThrow()
    {
        // Arrange
        var memoryGraph = new MemoryGraph(100);
        
        // Act
        var roots = _facade.GetRoots(memoryGraph);

        // Assert
        Assert.NotNull(roots);
        // Should not throw exception even with empty graph
    }

    [Fact]
    public void AllMethods_HandleNullMemoryGraphGracefully()
    {
        // Arrange
        object? memoryGraph = null;

        // Act & Assert
        var nodes = _facade.GetNodes(memoryGraph!);
        var edges = _facade.GetEdges(memoryGraph!);
        var roots = _facade.GetRoots(memoryGraph!);

        Assert.NotNull(nodes);
        Assert.Empty(nodes);
        
        Assert.NotNull(edges);
        Assert.Empty(edges);
        
        Assert.NotNull(roots);
        Assert.Empty(roots);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(3));
    }
}
