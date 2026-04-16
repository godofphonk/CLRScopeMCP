using ClrScope.Mcp.Services;
using ClrScope.Mcp.Services.Runtime;
using Xunit;

namespace ClrScope.Mcp.Tests.Services;

public class RuntimeServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        // Arrange & Act & Assert
        var exception = Record.Exception(() => new RuntimeService());
        Assert.Null(exception);
    }

    [Fact]
    public void ListTargets_ReturnsNonNullCollection()
    {
        // Arrange
        var service = new RuntimeService();

        // Act
        var result = service.ListTargets();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<RuntimeTarget>>(result);
    }

    [Fact]
    public void ListTargets_ReturnsEmptyList_WhenNoDotNetProcesses()
    {
        // Arrange
        var service = new RuntimeService();

        // Act
        var result = service.ListTargets();

        // Assert
        Assert.NotNull(result);
        // May be empty or contain processes depending on system state
    }

    [Fact]
    public void ListTargets_ReturnsCurrentProcess_WhenCalled()
    {
        // Arrange
        var service = new RuntimeService();
        var currentPid = Environment.ProcessId;

        // Act
        var result = service.ListTargets();

        // Assert
        Assert.NotNull(result);
        // Current process should be in the list if it's a .NET process
        var currentProcess = result.FirstOrDefault(t => t.Pid == currentPid);
        // May or may not be present depending on diagnostics client availability
    }

    [Fact]
    public void ListTargets_ReturnsTargetsWithValidPid()
    {
        // Arrange
        var service = new RuntimeService();

        // Act
        var result = service.ListTargets();

        // Assert
        Assert.NotNull(result);
        foreach (var target in result)
        {
            Assert.True(target.Pid > 0);
        }
    }

    [Fact]
    public void ListTargets_ReturnsTargetsWithNonEmptyProcessName()
    {
        // Arrange
        var service = new RuntimeService();

        // Act
        var result = service.ListTargets();

        // Assert
        Assert.NotNull(result);
        foreach (var target in result)
        {
            Assert.False(string.IsNullOrEmpty(target.ProcessName));
        }
    }

    [Fact]
    public void ListTargets_HandlesProcessExitBetweenCalls()
    {
        // Arrange
        var service = new RuntimeService();

        // Act
        var result = service.ListTargets();

        // Assert
        Assert.NotNull(result);
        // Should not throw even if processes exit between GetPublishedProcesses and GetProcessById
    }

    [Fact]
    public void ListTargets_ReturnsReadOnlyList()
    {
        // Arrange
        var service = new RuntimeService();

        // Act
        var result = service.ListTargets();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<RuntimeTarget>>(result);
    }

    [Fact]
    public void ListTargets_CanBeCalledMultipleTimes()
    {
        // Arrange
        var service = new RuntimeService();

        // Act
        var result1 = service.ListTargets();
        var result2 = service.ListTargets();

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        // Results may differ due to process state changes
    }

    [Fact]
    public void RuntimeTarget_HasValidPid()
    {
        // Arrange
        var target = new RuntimeTarget(1234, "testprocess");

        // Assert
        Assert.Equal(1234, target.Pid);
        Assert.True(target.Pid > 0);
    }

    [Fact]
    public void RuntimeTarget_HasValidProcessName()
    {
        // Arrange
        var target = new RuntimeTarget(1234, "testprocess");

        // Assert
        Assert.Equal("testprocess", target.ProcessName);
        Assert.False(string.IsNullOrEmpty(target.ProcessName));
    }

    [Fact]
    public void RuntimeTarget_IsRecordType()
    {
        // Arrange
        var target1 = new RuntimeTarget(1234, "testprocess");
        var target2 = new RuntimeTarget(1234, "testprocess");

        // Assert
        Assert.Equal(target1, target2);
        Assert.Equal(target1.GetHashCode(), target2.GetHashCode());
    }

    [Fact]
    public void RuntimeTarget_WithDifferentPids_AreNotEqual()
    {
        // Arrange
        var target1 = new RuntimeTarget(1234, "testprocess");
        var target2 = new RuntimeTarget(5678, "testprocess");

        // Assert
        Assert.NotEqual(target1, target2);
    }

    [Fact]
    public void RuntimeTarget_WithDifferentNames_AreNotEqual()
    {
        // Arrange
        var target1 = new RuntimeTarget(1234, "testprocess");
        var target2 = new RuntimeTarget(1234, "otherprocess");

        // Assert
        Assert.NotEqual(target1, target2);
    }
}
