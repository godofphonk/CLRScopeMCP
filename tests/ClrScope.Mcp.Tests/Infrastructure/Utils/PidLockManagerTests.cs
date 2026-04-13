using ClrScope.Mcp.Infrastructure;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Utils;

public class PidLockManagerTests : IDisposable
{
    private readonly PidLockManager _lockManager;

    public PidLockManagerTests()
    {
        _lockManager = new PidLockManager();
    }

    [Fact]
    public async Task AcquireLockAsync_ReturnsLockHandle_WhenPidIsProvided()
    {
        // Arrange
        var pid = 12345;

        // Act
        var lockHandle = await _lockManager.AcquireLockAsync(pid, CancellationToken.None);

        // Assert
        Assert.NotNull(lockHandle);
    }

    [Fact]
    public async Task AcquireLockAsync_AllowsMultipleLocksForSamePid_WhenReleased()
    {
        // Arrange
        var pid = 12345;

        // Act
        using (var lock1 = await _lockManager.AcquireLockAsync(pid, CancellationToken.None))
        {
            // Lock is held
        }

        // Lock should be released
        using (var lock2 = await _lockManager.AcquireLockAsync(pid, CancellationToken.None))
        {
            // Lock should be acquired again
            Assert.NotNull(lock2);
        }
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var lockManager = new PidLockManager();

        // Act & Assert
        var exception = Record.Exception(() => lockManager.Dispose());
        Assert.Null(exception);
    }

    public void Dispose()
    {
        _lockManager?.Dispose();
    }
}
