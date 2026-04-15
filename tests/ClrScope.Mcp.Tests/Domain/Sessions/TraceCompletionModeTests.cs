using ClrScope.Mcp.Domain.Sessions;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain.Sessions;

public class TraceCompletionModeTests
{
    [Fact]
    public void TraceCompletionMode_HasCompleteValue()
    {
        // Act & Assert
        Assert.Equal(0, (int)TraceCompletionMode.Complete);
    }

    [Fact]
    public void TraceCompletionMode_HasPartialValue()
    {
        // Act & Assert
        Assert.Equal(1, (int)TraceCompletionMode.Partial);
    }

    [Fact]
    public void TraceCompletionMode_HasCancelledValue()
    {
        // Act & Assert
        Assert.Equal(2, (int)TraceCompletionMode.Cancelled);
    }

    [Fact]
    public void TraceCompletionMode_HasFailedValue()
    {
        // Act & Assert
        Assert.Equal(3, (int)TraceCompletionMode.Failed);
    }

    [Fact]
    public void TraceCompletionMode_HasCompletedEarlyValue()
    {
        // Act & Assert
        Assert.Equal(4, (int)TraceCompletionMode.CompletedEarly);
    }

    [Fact]
    public void TraceCompletionMode_HasFiveValues()
    {
        // Act & Assert
        Assert.Equal(5, Enum.GetValues<TraceCompletionMode>().Length);
    }

    [Fact]
    public void TraceCompletionMode_AllValuesAreDistinct()
    {
        // Arrange
        var values = Enum.GetValues<TraceCompletionMode>();

        // Act & Assert
        Assert.Equal(values.Length, values.Cast<TraceCompletionMode>().Distinct().Count());
    }
}
