using ClrScope.Mcp.Domain.Artifacts;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain.Artifacts;

public class ArtifactKindTests
{
    [Fact]
    public void ArtifactKind_HasTraceValue()
    {
        // Act & Assert
        Assert.Equal(0, (int)ArtifactKind.Trace);
    }

    [Fact]
    public void ArtifactKind_HasDumpValue()
    {
        // Act & Assert
        Assert.Equal(1, (int)ArtifactKind.Dump);
    }

    [Fact]
    public void ArtifactKind_HasCountersValue()
    {
        // Act & Assert
        Assert.Equal(2, (int)ArtifactKind.Counters);
    }

    [Fact]
    public void ArtifactKind_HasGcDumpValue()
    {
        // Act & Assert
        Assert.Equal(3, (int)ArtifactKind.GcDump);
    }

    [Fact]
    public void ArtifactKind_HasStacksValue()
    {
        // Act & Assert
        Assert.Equal(4, (int)ArtifactKind.Stacks);
    }

    [Fact]
    public void ArtifactKind_HasFiveValues()
    {
        // Act & Assert
        Assert.Equal(5, Enum.GetValues<ArtifactKind>().Length);
    }

    [Fact]
    public void ArtifactKind_AllValuesAreDistinct()
    {
        // Arrange
        var values = Enum.GetValues<ArtifactKind>();

        // Act & Assert
        Assert.Equal(values.Length, values.Cast<ArtifactKind>().Distinct().Count());
    }
}
