using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Sessions;

public class ActiveOperationRegistryTests : IDisposable
{
    private readonly ActiveOperationRegistry _registry;

    public ActiveOperationRegistryTests()
    {
        var loggerMock = new Mock<ILogger<ActiveOperationRegistry>>();
        _registry = new ActiveOperationRegistry(loggerMock.Object);
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenLoggerProvided()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ActiveOperationRegistry>>();

        // Act & Assert
        var exception = Record.Exception(() => new ActiveOperationRegistry(loggerMock.Object));
        Assert.Null(exception);
    }

    [Fact]
    public void TryRegister_ReturnsTrue_WhenSessionNotRegistered()
    {
        // Arrange
        var sessionId = SessionId.New();
        var cts = new CancellationTokenSource();

        // Act
        var result = _registry.TryRegister(sessionId, cts);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryRegister_ReturnsFalse_WhenSessionAlreadyRegistered()
    {
        // Arrange
        var sessionId = SessionId.New();
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();

        // Act
        _registry.TryRegister(sessionId, cts1);
        var result = _registry.TryRegister(sessionId, cts2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryCancel_ReturnsTrue_WhenSessionRegistered()
    {
        // Arrange
        var sessionId = SessionId.New();
        var cts = new CancellationTokenSource();
        _registry.TryRegister(sessionId, cts);

        // Act
        var result = _registry.TryCancel(sessionId, "test cancellation");

        // Assert
        Assert.True(result);
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void TryCancel_ReturnsFalse_WhenSessionNotRegistered()
    {
        // Arrange
        var sessionId = SessionId.New();

        // Act
        var result = _registry.TryCancel(sessionId, "test cancellation");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Complete_DoesNotThrow_WhenSessionRegistered()
    {
        // Arrange
        var sessionId = SessionId.New();
        var cts = new CancellationTokenSource();
        _registry.TryRegister(sessionId, cts);

        // Act & Assert
        var exception = Record.Exception(() => _registry.Complete(sessionId));
        Assert.Null(exception);
    }

    [Fact]
    public void Complete_DoesNotThrow_WhenSessionNotRegistered()
    {
        // Arrange
        var sessionId = SessionId.New();

        // Act & Assert
        var exception = Record.Exception(() => _registry.Complete(sessionId));
        Assert.Null(exception);
    }

    public void Dispose()
    {
        _registry?.Dispose();
    }
}
