using ClrScope.Mcp.Domain.Sessions;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain.Sessions;

public class SessionKindTests
{
    [Fact]
    public void SessionKind_HasTraceValue()
    {
        // Act & Assert
        Assert.Equal(0, (int)SessionKind.Trace);
    }

    [Fact]
    public void SessionKind_HasDumpValue()
    {
        // Act & Assert
        Assert.Equal(1, (int)SessionKind.Dump);
    }

    [Fact]
    public void SessionKind_HasCountersValue()
    {
        // Act & Assert
        Assert.Equal(2, (int)SessionKind.Counters);
    }

    [Fact]
    public void SessionKind_HasGcDumpValue()
    {
        // Act & Assert
        Assert.Equal(3, (int)SessionKind.GcDump);
    }

    [Fact]
    public void SessionKind_HasStacksValue()
    {
        // Act & Assert
        Assert.Equal(4, (int)SessionKind.Stacks);
    }

    [Fact]
    public void SessionKind_HasFiveValues()
    {
        // Act & Assert
        Assert.Equal(5, Enum.GetValues<SessionKind>().Length);
    }

    [Fact]
    public void SessionKind_AllValuesAreDistinct()
    {
        // Arrange
        var values = Enum.GetValues<SessionKind>();

        // Act & Assert
        Assert.Equal(values.Length, values.Cast<SessionKind>().Distinct().Count());
    }
}
