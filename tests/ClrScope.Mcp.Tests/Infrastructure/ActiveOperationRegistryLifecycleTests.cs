using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure;

public class ActiveOperationRegistryLifecycleTests : IDisposable
{
    private readonly Mock<ILogger<ActiveOperationRegistry>> _loggerMock;
    private readonly ActiveOperationRegistry _registry;

    public ActiveOperationRegistryLifecycleTests()
    {
        _loggerMock = new Mock<ILogger<ActiveOperationRegistry>>();
        _registry = new ActiveOperationRegistry(_loggerMock.Object);
    }

    public void Dispose()
    {
        _registry.Dispose();
    }

    [Fact]
    public void Complete_DisposesCancellationTokenSource()
    {
        // Arrange
        var sessionId = new SessionId(Guid.NewGuid().ToString());
        var cts = new CancellationTokenSource();
        _registry.TryRegister(sessionId, cts);

        // Act
        _registry.Complete(sessionId);

        // Assert - CancellationTokenSource should be disposed
        Assert.Throws<ObjectDisposedException>(() => cts.Cancel());
    }

    [Fact]
    public void TryCancel_DisposesCancellationTokenSource()
    {
        // Arrange
        var sessionId = new SessionId(Guid.NewGuid().ToString());
        var cts = new CancellationTokenSource();
        _registry.TryRegister(sessionId, cts);

        // Act
        _registry.TryCancel(sessionId, "test cancellation");

        // Assert - CancellationTokenSource should be disposed
        Assert.Throws<ObjectDisposedException>(() => cts.Cancel());
    }

    [Fact]
    public void TryCancel_RemovesFromRegistry()
    {
        // Arrange
        var sessionId = new SessionId(Guid.NewGuid().ToString());
        var cts = new CancellationTokenSource();
        _registry.TryRegister(sessionId, cts);

        // Act
        _registry.TryCancel(sessionId, "test cancellation");

        // Try to cancel again - should fail because it was removed
        var result = _registry.TryCancel(sessionId, "second cancellation");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Complete_RemovesFromRegistry()
    {
        // Arrange
        var sessionId = new SessionId(Guid.NewGuid().ToString());
        var cts = new CancellationTokenSource();
        _registry.TryRegister(sessionId, cts);

        // Act
        _registry.Complete(sessionId);

        // Try to cancel - should fail because it was removed
        var result = _registry.TryCancel(sessionId, "cancellation after complete");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Dispose_CancelsAllActiveOperations()
    {
        // Arrange
        var sessionId1 = new SessionId(Guid.NewGuid().ToString());
        var sessionId2 = new SessionId(Guid.NewGuid().ToString());
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
        _registry.TryRegister(sessionId1, cts1);
        _registry.TryRegister(sessionId2, cts2);

        // Act
        _registry.Dispose();

        // Assert - all operations should be cancelled
        Assert.True(cts1.IsCancellationRequested);
        Assert.True(cts2.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_DisposesAllCancellationTokenSources()
    {
        // Arrange
        var sessionId1 = new SessionId(Guid.NewGuid().ToString());
        var sessionId2 = new SessionId(Guid.NewGuid().ToString());
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
        _registry.TryRegister(sessionId1, cts1);
        _registry.TryRegister(sessionId2, cts2);

        // Act
        _registry.Dispose();

        // Assert - all CancellationTokenSources should be disposed
        Assert.Throws<ObjectDisposedException>(() => cts1.Cancel());
        Assert.Throws<ObjectDisposedException>(() => cts2.Cancel());
    }

    [Fact]
    public void Dispose_ClearsRegistry()
    {
        // Arrange
        var sessionId1 = new SessionId(Guid.NewGuid().ToString());
        var sessionId2 = new SessionId(Guid.NewGuid().ToString());
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
        _registry.TryRegister(sessionId1, cts1);
        _registry.TryRegister(sessionId2, cts2);

        // Act
        _registry.Dispose();

        // Try to cancel - should fail because registry was cleared
        var result = _registry.TryCancel(sessionId1, "cancellation after dispose");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        // Arrange
        var sessionId = new SessionId(Guid.NewGuid().ToString());
        var cts = new CancellationTokenSource();
        _registry.TryRegister(sessionId, cts);

        // Act - dispose multiple times
        _registry.Dispose();
        _registry.Dispose();
        _registry.Dispose();

        // Assert - should not throw
    }

    [Fact]
    public void TryRegister_DuplicateSessionId_ReturnsFalse()
    {
        // Arrange
        var sessionId = new SessionId(Guid.NewGuid().ToString());
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();

        // Act
        var result1 = _registry.TryRegister(sessionId, cts1);
        var result2 = _registry.TryRegister(sessionId, cts2);

        // Assert
        Assert.True(result1);
        Assert.False(result2);
    }

    [Fact]
    public void TryCancel_NonExistentSession_ReturnsFalse()
    {
        // Arrange
        var sessionId = new SessionId(Guid.NewGuid().ToString());

        // Act
        var result = _registry.TryCancel(sessionId, "test cancellation");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Complete_NonExistentSession_DoesNotThrow()
    {
        // Arrange
        var sessionId = new SessionId(Guid.NewGuid().ToString());

        // Act & Assert - should not throw
        _registry.Complete(sessionId);
    }

    [Fact]
    public void MultipleOperations_Lifecycle_MaintainsCorrectState()
    {
        // Arrange
        var sessionId1 = new SessionId(Guid.NewGuid().ToString());
        var sessionId2 = new SessionId(Guid.NewGuid().ToString());
        var sessionId3 = new SessionId(Guid.NewGuid().ToString());
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
        var cts3 = new CancellationTokenSource();

        _registry.TryRegister(sessionId1, cts1);
        _registry.TryRegister(sessionId2, cts2);
        _registry.TryRegister(sessionId3, cts3);

        // Act - complete one, cancel one, leave one
        _registry.Complete(sessionId1);
        _registry.TryCancel(sessionId2, "test cancellation");

        // Assert
        Assert.Throws<ObjectDisposedException>(() => cts1.Cancel()); // Completed and disposed
        Assert.Throws<ObjectDisposedException>(() => cts2.Cancel()); // Cancelled and disposed
        Assert.False(cts3.IsCancellationRequested); // Still active

        // Cleanup
        _registry.Dispose();
    }
}
