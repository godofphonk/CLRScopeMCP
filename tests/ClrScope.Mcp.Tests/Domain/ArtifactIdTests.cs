using ClrScope.Mcp.Domain.Artifacts;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain;

public class ArtifactIdTests
{
    [Fact]
    public void New_ReturnsArtifactIdWithPrefix()
    {
        // Act
        var artifactId = ArtifactId.New();

        // Assert
        Assert.StartsWith("art_", artifactId.Value);
    }

    [Fact]
    public void New_ReturnsDifferentIds()
    {
        // Act
        var id1 = ArtifactId.New();
        var id2 = ArtifactId.New();

        // Assert
        Assert.NotEqual(id1.Value, id2.Value);
    }

    [Fact]
    public void Constructor_AcceptsValidString()
    {
        // Arrange
        var value = "art_test123";

        // Act
        var artifactId = new ArtifactId(value);

        // Assert
        Assert.Equal(value, artifactId.Value);
    }

    [Fact]
    public void Value_ReturnsOriginalString()
    {
        // Arrange
        var value = "art_abc123";
        var artifactId = new ArtifactId(value);

        // Act
        var result = artifactId.Value;

        // Assert
        Assert.Equal(value, result);
    }
}
