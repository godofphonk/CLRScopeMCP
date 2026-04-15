using ClrScope.Mcp.Domain.Artifacts;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain.Artifacts;

public class ArtifactStatusTests
{
    [Fact]
    public void ArtifactStatus_HasPendingValue()
    {
        // Act & Assert
        Assert.Equal(0, (int)ArtifactStatus.Pending);
    }

    [Fact]
    public void ArtifactStatus_HasCompletedValue()
    {
        // Act & Assert
        Assert.Equal(1, (int)ArtifactStatus.Completed);
    }

    [Fact]
    public void ArtifactStatus_HasPartialValue()
    {
        // Act & Assert
        Assert.Equal(2, (int)ArtifactStatus.Partial);
    }

    [Fact]
    public void ArtifactStatus_HasFailedValue()
    {
        // Act & Assert
        Assert.Equal(3, (int)ArtifactStatus.Failed);
    }

    [Fact]
    public void ArtifactStatus_HasCancelledValue()
    {
        // Act & Assert
        Assert.Equal(4, (int)ArtifactStatus.Cancelled);
    }

    [Fact]
    public void ArtifactStatus_HasDeletedValue()
    {
        // Act & Assert
        Assert.Equal(5, (int)ArtifactStatus.Deleted);
    }

    [Fact]
    public void ArtifactStatus_HasSixValues()
    {
        // Act & Assert
        Assert.Equal(6, Enum.GetValues<ArtifactStatus>().Length);
    }

    [Fact]
    public void ArtifactStatus_AllValuesAreDistinct()
    {
        // Arrange
        var values = Enum.GetValues<ArtifactStatus>();

        // Act & Assert
        Assert.Equal(values.Length, values.Cast<ArtifactStatus>().Distinct().Count());
    }
}
