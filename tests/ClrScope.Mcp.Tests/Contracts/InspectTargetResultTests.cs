using ClrScope.Mcp.Contracts;
using Xunit;

namespace ClrScope.Mcp.Tests.Contracts;

public class InspectTargetResultTests
{
    [Fact]
    public void InspectTargetResult_CreatesWithValidParameters()
    {
        // Arrange
        var details = new RuntimeTargetDetails(
            1234,
            "test-process",
            "command line args",
            "Linux",
            "x64"
        );
        var warnings = new[] { "warning1", "warning2" };

        // Act
        var result = new InspectTargetResult(
            Found: true,
            Attachable: true,
            Details: details,
            Warnings: warnings,
            Error: null
        );

        // Assert
        Assert.True(result.Found);
        Assert.True(result.Attachable);
        Assert.Equal(details, result.Details);
        Assert.Equal(warnings, result.Warnings);
        Assert.Null(result.Error);
    }

    [Fact]
    public void InspectTargetResult_NotFound_CreatesWithDefaultValues()
    {
        // Act
        var result = InspectTargetResult.NotFound("Process not found");

        // Assert
        Assert.False(result.Found);
        Assert.False(result.Attachable);
        Assert.Null(result.Details);
        Assert.Empty(result.Warnings);
        Assert.Equal("Process not found", result.Error);
    }

    [Fact]
    public void InspectTargetResult_NotFound_WithoutError_CreatesWithNullError()
    {
        // Act
        var result = InspectTargetResult.NotFound();

        // Assert
        Assert.False(result.Found);
        Assert.False(result.Attachable);
        Assert.Null(result.Details);
        Assert.Empty(result.Warnings);
        Assert.Null(result.Error);
    }

    [Fact]
    public void InspectTargetResult_NotAttachable_CreatesWithReasonAndWarnings()
    {
        // Arrange
        var warnings = new[] { "warning1", "warning2" };

        // Act
        var result = InspectTargetResult.NotAttachable("Diagnostics disabled", warnings);

        // Assert
        Assert.True(result.Found);
        Assert.False(result.Attachable);
        Assert.Null(result.Details);
        Assert.Equal(warnings, result.Warnings);
        Assert.Equal("Diagnostics disabled", result.Error);
    }

    [Fact]
    public void InspectTargetResult_Success_CreatesWithDetailsAndWarnings()
    {
        // Arrange
        var details = new RuntimeTargetDetails(
            1234,
            "test-process",
            "command line args",
            "Linux",
            "x64"
        );
        var warnings = new[] { "warning1" };

        // Act
        var result = InspectTargetResult.Success(details, warnings);

        // Assert
        Assert.True(result.Found);
        Assert.True(result.Attachable);
        Assert.Equal(details, result.Details);
        Assert.Equal(warnings, result.Warnings);
        Assert.Null(result.Error);
    }

    #region RuntimeTargetDetails Tests

    [Fact]
    public void RuntimeTargetDetails_CreatesWithValidParameters()
    {
        // Act
        var details = new RuntimeTargetDetails(
            1234,
            "test-process",
            "command line args",
            "Linux",
            "x64"
        );

        // Assert
        Assert.Equal(1234, details.Pid);
        Assert.Equal("test-process", details.ProcessName);
        Assert.Equal("command line args", details.CommandLine);
        Assert.Equal("Linux", details.OperatingSystem);
        Assert.Equal("x64", details.ProcessArchitecture);
    }

    [Fact]
    public void RuntimeTargetDetails_CommandLine_CanBeNull()
    {
        // Act
        var details = new RuntimeTargetDetails(
            1234,
            "test-process",
            null,
            "Linux",
            "x64"
        );

        // Assert
        Assert.Null(details.CommandLine);
    }

    #endregion
}
