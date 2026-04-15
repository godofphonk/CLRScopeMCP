using ClrScope.Mcp.Tools.Runtime;
using Xunit;

namespace ClrScope.Mcp.Tests.Tools.Runtime;

public class RuntimeToolsTests
{
    #region InspectTarget Tests

    [Fact]
    public void InspectTarget_ReturnsError_WhenPidIsZero()
    {
        // Arrange
        var pid = 0;

        // Act
        var result = RuntimeTools.InspectTarget(pid, null!);

        // Assert
        Assert.False(result.Found);
        Assert.False(result.Attachable);
        Assert.Equal("Process ID must be greater than 0", result.Error);
    }

    [Fact]
    public void InspectTarget_ReturnsError_WhenPidIsNegative()
    {
        // Arrange
        var pid = -1;

        // Act
        var result = RuntimeTools.InspectTarget(pid, null!);

        // Assert
        Assert.False(result.Found);
        Assert.False(result.Attachable);
        Assert.Equal("Process ID must be greater than 0", result.Error);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-999)]
    [InlineData(-1)]
    [InlineData(0)]
    public void InspectTarget_ReturnsError_WhenPidIsLessThanOrEqualToZero(int pid)
    {
        // Act
        var result = RuntimeTools.InspectTarget(pid, null!);

        // Assert
        Assert.False(result.Found);
        Assert.False(result.Attachable);
        Assert.Equal("Process ID must be greater than 0", result.Error);
    }

    #endregion

    #region ListTargets Tests

    [Fact]
    public void ListTargets_ReturnsError_WhenSortByIsInvalid()
    {
        // Arrange
        var sortBy = "invalid";

        // Act
        var result = RuntimeTools.ListTargets(null!, sortBy: sortBy);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Contains("SortBy must be one of", result.Error);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("random")]
    [InlineData("test")]
    public void ListTargets_RejectsInvalidSortBy(string sortBy)
    {
        // Act
        var result = RuntimeTools.ListTargets(null!, sortBy: sortBy);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Contains("SortBy must be one of", result.Error);
    }

    #endregion
}
