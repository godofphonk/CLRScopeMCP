using ClrScope.Mcp.Domain.Artifacts;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain;

public class CleanupArtifactsResultTests
{
    [Fact]
    public void Constructor_CreatesResult_WithProvidedValues()
    {
        // Arrange
        var deletedCount = 5;
        var message = "Test message";

        // Act
        var result = new CleanupArtifactsResult(deletedCount, message);

        // Assert
        Assert.Equal(deletedCount, result.DeletedCount);
        Assert.Equal(message, result.Message);
    }
}
