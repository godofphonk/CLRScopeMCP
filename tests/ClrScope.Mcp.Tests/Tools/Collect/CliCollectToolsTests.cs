using ClrScope.Mcp.Tools.Collect;
using Xunit;

namespace ClrScope.Mcp.Tests.Tools.Collect;

public class CliCollectToolsTests
{
    #region CollectCounters Tests

    [Fact]
    public async Task CollectCounters_ReturnsError_WhenPidIsZero()
    {
        // Arrange
        var pid = 0;

        // Act
        var result = await CollectCountersTools.CollectCounters(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    [Fact]
    public async Task CollectCounters_ReturnsError_WhenPidIsNegative()
    {
        // Arrange
        var pid = -1;

        // Act
        var result = await CollectCountersTools.CollectCounters(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    [Fact]
    public async Task CollectCounters_ReturnsError_WhenDurationIsEmpty()
    {
        // Arrange
        var pid = 1234;
        var duration = "";

        // Act
        var result = await CollectCountersTools.CollectCounters(pid, null!, duration: duration);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Duration must not be empty", result.Message);
    }

    [Fact]
    public async Task CollectCounters_ReturnsError_WhenDurationIsWhitespace()
    {
        // Arrange
        var pid = 1234;
        var duration = "   ";

        // Act
        var result = await CollectCountersTools.CollectCounters(pid, null!, duration: duration);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Duration must not be empty", result.Message);
    }

    [Fact]
    public async Task CollectCounters_ReturnsError_WhenDurationIsInvalidFormat()
    {
        // Arrange
        var pid = 1234;
        var duration = "invalid";

        // Act
        var result = await CollectCountersTools.CollectCounters(pid, null!, duration: duration);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Duration must be in hh:mm:ss format (e.g., 00:01:00)", result.Message);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-999)]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task CollectCounters_ReturnsError_WhenPidIsLessThanOrEqualToZero(int pid)
    {
        // Act
        var result = await CollectCountersTools.CollectCounters(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    #endregion

    #region CollectGcDump Tests

    [Fact]
    public async Task CollectGcDump_ReturnsError_WhenPidIsZero()
    {
        // Arrange
        var pid = 0;

        // Act
        var result = await CollectCountersTools.CollectGcDump(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    [Fact]
    public async Task CollectGcDump_ReturnsError_WhenPidIsNegative()
    {
        // Arrange
        var pid = -1;

        // Act
        var result = await CollectCountersTools.CollectGcDump(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-999)]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task CollectGcDump_ReturnsError_WhenPidIsLessThanOrEqualToZero(int pid)
    {
        // Act
        var result = await CollectCountersTools.CollectGcDump(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    #endregion

    #region CollectStacks Tests

    [Fact]
    public async Task CollectStacks_ReturnsError_WhenPidIsZero()
    {
        // Arrange
        var pid = 0;
        var format = "text";

        // Act
        var result = await CollectCountersTools.CollectStacks(pid, null!, format);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    [Fact]
    public async Task CollectStacks_ReturnsError_WhenPidIsNegative()
    {
        // Arrange
        var pid = -1;
        var format = "text";

        // Act
        var result = await CollectCountersTools.CollectStacks(pid, null!, format);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    [Fact]
    public async Task CollectStacks_ReturnsError_WhenFormatIsInvalid()
    {
        // Arrange
        var pid = 1234;
        var format = "invalid";

        // Act
        var result = await CollectCountersTools.CollectStacks(pid, null!, format);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Format must be 'text' or 'json'", result.Message);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-999)]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task CollectStacks_ReturnsError_WhenPidIsLessThanOrEqualToZero(int pid)
    {
        // Act
        var result = await CollectCountersTools.CollectStacks(pid, null!, "text");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Message);
    }

    #endregion
}
