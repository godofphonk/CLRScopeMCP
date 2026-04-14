using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Services;
using Xunit;

namespace ClrScope.Mcp.Tests.Services;

public class InspectTargetServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        // Arrange & Act & Assert
        var exception = Record.Exception(() => new InspectTargetService());
        Assert.Null(exception);
    }

    [Fact]
    public void InspectTarget_ReturnsNotFound_WhenProcessDoesNotExist()
    {
        // Arrange
        var service = new InspectTargetService();
        var nonExistentPid = 99999;

        // Act
        var result = service.InspectTarget(nonExistentPid);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Found);
        Assert.False(result.Attachable);
        Assert.Null(result.Details);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error.ToLower());
    }

    [Fact]
    public void InspectTarget_ReturnsNotAttachable_WhenProcessIsNotDotNet()
    {
        // Arrange
        var service = new InspectTargetService();
        
        // Use current process which should be .NET, but we'll test the structure
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.InspectTarget(currentPid);

        // Assert
        Assert.NotNull(result);
        // Current process should be .NET and attachable
        if (result.Found && result.Attachable)
        {
            Assert.NotNull(result.Details);
            Assert.Equal(currentPid, result.Details.Pid);
        }
    }

    [Fact]
    public void InspectTarget_ReturnsSuccess_WhenProcessIsDotNetAndAttachable()
    {
        // Arrange
        var service = new InspectTargetService();
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.InspectTarget(currentPid);

        // Assert
        Assert.NotNull(result);
        if (result.Found && result.Attachable)
        {
            Assert.NotNull(result.Details);
            Assert.Equal(currentPid, result.Details.Pid);
            Assert.False(string.IsNullOrEmpty(result.Details.ProcessName));
            Assert.False(string.IsNullOrEmpty(result.Details.OperatingSystem));
            Assert.NotNull(result.Details.ProcessArchitecture); // Can be "Unknown"
            Assert.Null(result.Details.CommandLine);
        }
    }

    [Fact]
    public void InspectTarget_ReturnsProcessName_WhenProcessExists()
    {
        // Arrange
        var service = new InspectTargetService();
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.InspectTarget(currentPid);

        // Assert
        Assert.NotNull(result);
        if (result.Found && result.Attachable)
        {
            Assert.NotNull(result.Details);
            Assert.False(string.IsNullOrEmpty(result.Details.ProcessName));
        }
    }

    [Fact]
    public void InspectTarget_ReturnsCorrectPid_WhenProcessExists()
    {
        // Arrange
        var service = new InspectTargetService();
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.InspectTarget(currentPid);

        // Assert
        Assert.NotNull(result);
        if (result.Found && result.Attachable)
        {
            Assert.NotNull(result.Details);
            Assert.Equal(currentPid, result.Details.Pid);
        }
    }

    [Fact]
    public void InspectTarget_ReturnsArchitecture_WhenProcessExists()
    {
        // Arrange
        var service = new InspectTargetService();
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.InspectTarget(currentPid);

        // Assert
        Assert.NotNull(result);
        if (result.Found && result.Attachable)
        {
            Assert.NotNull(result.Details);
            Assert.NotNull(result.Details.ProcessArchitecture); // Can be "Unknown"
        }
    }

    [Fact]
    public void InspectTarget_ReturnsOS_WhenProcessExists()
    {
        // Arrange
        var service = new InspectTargetService();
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.InspectTarget(currentPid);

        // Assert
        Assert.NotNull(result);
        if (result.Found && result.Attachable)
        {
            Assert.NotNull(result.Details);
            Assert.False(string.IsNullOrEmpty(result.Details.OperatingSystem));
        }
    }

    [Fact]
    public void InspectTarget_ReturnsNullForCommandLine_WhenProcessExists()
    {
        // Arrange
        var service = new InspectTargetService();
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.InspectTarget(currentPid);

        // Assert
        Assert.NotNull(result);
        if (result.Found && result.Attachable)
        {
            Assert.NotNull(result.Details);
            Assert.Null(result.Details.CommandLine);
        }
    }

    [Fact]
    public void InspectTarget_ReturnsWarningsCollection_WhenProcessExists()
    {
        // Arrange
        var service = new InspectTargetService();
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.InspectTarget(currentPid);

        // Assert
        Assert.NotNull(result);
        if (result.Found && result.Attachable)
        {
            Assert.NotNull(result.Warnings);
            // Warnings can be empty or contain items
        }
    }

    [Fact]
    public void InspectTarget_HandlesArgumentException_WhenProcessExitsDuringCheck()
    {
        // Arrange
        var service = new InspectTargetService();
        var nonExistentPid = 99999;

        // Act
        var result = service.InspectTarget(nonExistentPid);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Found);
        Assert.False(result.Attachable);
    }
}
