using ClrScope.Mcp.Tools.Collect;
using Xunit;

namespace ClrScope.Mcp.Tests.Tools.Collect;

public class CollectToolsTests
{
    #region CollectDump Tests

    [Fact]
    public async Task CollectDump_ReturnsError_WhenPidIsZero()
    {
        // Arrange
        var pid = 0;

        // Act
        var result = await CollectTools.CollectDump(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Error);
    }

    [Fact]
    public async Task CollectDump_ReturnsError_WhenPidIsNegative()
    {
        // Arrange
        var pid = -1;

        // Act
        var result = await CollectTools.CollectDump(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Error);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-999)]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task CollectDump_ReturnsError_WhenPidIsLessThanOrEqualToZero(int pid)
    {
        // Act
        var result = await CollectTools.CollectDump(pid, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Error);
    }

    #endregion

    #region CollectTrace Tests

    [Fact]
    public async Task CollectTrace_ReturnsError_WhenPidIsZero()
    {
        // Arrange
        var pid = 0;
        var duration = "00:01:00";

        // Act
        var result = await CollectTools.CollectTrace(pid, duration, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Error);
    }

    [Fact]
    public async Task CollectTrace_ReturnsError_WhenPidIsNegative()
    {
        // Arrange
        var pid = -1;
        var duration = "00:01:00";

        // Act
        var result = await CollectTools.CollectTrace(pid, duration, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Process ID must be greater than 0", result.Error);
    }

    [Fact]
    public async Task CollectTrace_ReturnsError_WhenDurationIsEmpty()
    {
        // Arrange
        var pid = 1234;
        var duration = "";

        // Act
        var result = await CollectTools.CollectTrace(pid, duration, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Duration must not be empty", result.Error);
    }

    [Fact]
    public async Task CollectTrace_ReturnsError_WhenDurationIsWhitespace()
    {
        // Arrange
        var pid = 1234;
        var duration = "   ";

        // Act
        var result = await CollectTools.CollectTrace(pid, duration, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Duration must not be empty", result.Error);
    }

    [Fact]
    public async Task CollectTrace_ReturnsError_WhenDurationIsInvalidFormat()
    {
        // Arrange
        var pid = 1234;
        var duration = "invalid";

        // Act
        var result = await CollectTools.CollectTrace(pid, duration, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Duration must be in hh:mm:ss format (e.g., 00:01:30)", result.Error);
    }

    [Fact]
    public async Task CollectTrace_ReturnsError_WhenProfileIsInvalid()
    {
        // Arrange
        var pid = 1234;
        var duration = "00:01:00";
        var profile = "invalid";

        // Act
        var result = await CollectTools.CollectTrace(pid, duration, null!, profile: profile);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Profile must be one of", result.Error);
    }

    [Fact]
    public async Task CollectTrace_ReturnsError_WhenCustomProviderIsEmpty()
    {
        // Arrange
        var pid = 1234;
        var duration = "00:01:00";
        var customProviders = new[] { "" };

        // Act
        var result = await CollectTools.CollectTrace(pid, duration, null!, customProviders: customProviders);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Custom providers must not be empty", result.Error);
    }

    [Fact]
    public async Task CollectTrace_ReturnsError_WhenCustomProviderFormatIsInvalid()
    {
        // Arrange
        var pid = 1234;
        var duration = "00:01:00";
        var customProviders = new[] { "InvalidFormat" };

        // Act
        var result = await CollectTools.CollectTrace(pid, duration, null!, customProviders: customProviders);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("must be in format 'ProviderName:Level:Keywords'", result.Error);
    }

    #endregion
}
