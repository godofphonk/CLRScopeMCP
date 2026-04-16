using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Services.Heap;

public class EventPipeHeapGraphSourceAdapterTests
{
    private readonly Mock<ILogger<EventPipeHeapGraphSourceAdapter>> _loggerMock;
    private readonly EventPipeHeapGraphSourceAdapter _adapter;
    private readonly string _testDataPath;

    public EventPipeHeapGraphSourceAdapterTests()
    {
        _loggerMock = new Mock<ILogger<EventPipeHeapGraphSourceAdapter>>();
        _adapter = new EventPipeHeapGraphSourceAdapter(_loggerMock.Object);
        _testDataPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "test-data"));
    }

    private string GetTestDataPath(string fileName) => Path.Combine(_testDataPath, fileName);

    [Fact]
    public async Task ReadAsync_WithValidNettraceFile_ReturnsMemoryGraphEnvelope()
    {
        // Arrange
        var nettracePath = GetTestDataPath("test-data.nettrace");

        // Act
        var result = await _adapter.ReadAsync(nettracePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.MemoryGraph);
        Assert.NotNull(result.HeapInfo);
    }

    [Fact]
    public async Task ReadAsync_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nettracePath = "/nonexistent/file.nettrace";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _adapter.ReadAsync(nettracePath, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_WithInvalidExtension_ThrowsException()
    {
        // Arrange
        var invalidPath = GetTestDataPath("test-data.txt");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => _adapter.ReadAsync(invalidPath, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_VerifyMemoryGraphIsNotNull()
    {
        // Arrange
        var nettracePath = GetTestDataPath("test-data.nettrace");

        // Act
        var result = await _adapter.ReadAsync(nettracePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.MemoryGraph);
        // This should not hang - if it hangs, there's a deadlock in EventPipeHeapGraphSourceAdapter
    }

    [Fact]
    public async Task ReadAsync_VerifyHeapInfoIsNotNull()
    {
        // Arrange
        var nettracePath = GetTestDataPath("test-data.nettrace");

        // Act
        var result = await _adapter.ReadAsync(nettracePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.HeapInfo);
        Assert.NotNull(result.HeapInfo.Segments);
    }

    [Fact]
    public async Task ReadAsync_VerifyDoesNotHang()
    {
        // Arrange
        var nettracePath = GetTestDataPath("test-data.nettrace");
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout

        // Act
        var task = _adapter.ReadAsync(nettracePath, cts.Token);

        // Wait for completion or timeout
        var completed = await Task.WhenAny(task, Task.Delay(30000));

        // Assert
        Assert.True(task.IsCompleted, "ReadAsync did not complete within 30 seconds - likely deadlock");

        if (task.IsCompleted)
        {
            var result = await task;
            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ReadAsync_VerifyLogMessagesAreLogged()
    {
        // Arrange
        var nettracePath = GetTestDataPath("test-data.nettrace");

        // Act
        await _adapter.ReadAsync(nettracePath, CancellationToken.None);

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
    public async Task ReadAsync_VerifyNettraceFileExistsAndIsReadable()
    {
        // Arrange
        var nettracePath = GetTestDataPath("test-data.nettrace");

        // Assert - Verify file exists and is readable
        Assert.True(File.Exists(nettracePath), $"Nettrace file not found: {nettracePath}");

        var fileInfo = new FileInfo(nettracePath);
        Assert.True(fileInfo.Length > 0, $"Nettrace file is empty: {nettracePath}");

        // Verify file can be read
        using var stream = File.OpenRead(nettracePath);
        Assert.NotNull(stream);
    }

    [Fact]
    public async Task ReadAsync_WithCancelledCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var nettracePath = GetTestDataPath("test-data.nettrace");
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _adapter.ReadAsync(nettracePath, cts.Token));
    }

    [Fact]
    public async Task ReadAsync_WithShortTimeout_ThrowsOperationCanceledException()
    {
        // Arrange
        var nettracePath = GetTestDataPath("test-data.nettrace");
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1)); // Very short timeout

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _adapter.ReadAsync(nettracePath, cts.Token));
    }
}
