using ClrScope.Mcp.Domain.Sessions;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain.Sessions;

public class SessionStatusTests
{
    [Fact]
    public void SessionStatus_HasPendingValue()
    {
        // Act & Assert
        Assert.Equal(0, (int)SessionStatus.Pending);
    }

    [Fact]
    public void SessionStatus_HasRunningValue()
    {
        // Act & Assert
        Assert.Equal(1, (int)SessionStatus.Running);
    }

    [Fact]
    public void SessionStatus_HasCompletedValue()
    {
        // Act & Assert
        Assert.Equal(2, (int)SessionStatus.Completed);
    }

    [Fact]
    public void SessionStatus_HasFailedValue()
    {
        // Act & Assert
        Assert.Equal(3, (int)SessionStatus.Failed);
    }

    [Fact]
    public void SessionStatus_HasCancelledValue()
    {
        // Act & Assert
        Assert.Equal(4, (int)SessionStatus.Cancelled);
    }

    [Fact]
    public void SessionStatus_HasFiveValues()
    {
        // Act & Assert
        Assert.Equal(5, Enum.GetValues<SessionStatus>().Length);
    }

    [Fact]
    public void SessionStatus_AllValuesAreDistinct()
    {
        // Arrange
        var values = Enum.GetValues<SessionStatus>();

        // Act & Assert
        Assert.Equal(values.Length, values.Cast<SessionStatus>().Distinct().Count());
    }
}
