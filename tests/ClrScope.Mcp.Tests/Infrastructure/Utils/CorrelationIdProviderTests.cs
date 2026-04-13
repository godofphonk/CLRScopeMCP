using ClrScope.Mcp.Infrastructure;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Utils;

public class CorrelationIdProviderTests
{
    [Fact]
    public void GenerateCorrelationId_ReturnsValidId()
    {
        // Arrange
        var provider = new CorrelationIdProvider();

        // Act
        var correlationId = provider.GenerateCorrelationId();

        // Assert
        Assert.NotNull(correlationId);
        Assert.Equal(16, correlationId.Length);
    }

    [Fact]
    public void GetCorrelationId_ReturnsGeneratedId_WhenSet()
    {
        // Arrange
        var provider = new CorrelationIdProvider();
        var expectedId = provider.GenerateCorrelationId();

        // Act
        var actualId = provider.GetCorrelationId();

        // Assert
        Assert.Equal(expectedId, actualId);
    }

    [Fact]
    public void GetCorrelationId_GeneratesNewId_WhenNotSet()
    {
        // Arrange
        var provider = new CorrelationIdProvider();

        // Act
        var correlationId = provider.GetCorrelationId();

        // Assert
        Assert.NotNull(correlationId);
        Assert.Equal(16, correlationId.Length);
    }

    [Fact]
    public void SetCorrelationId_SetsTheId()
    {
        // Arrange
        var provider = new CorrelationIdProvider();
        var expectedId = "test-correlation-id";

        // Act
        provider.SetCorrelationId(expectedId);
        var actualId = provider.GetCorrelationId();

        // Assert
        Assert.Equal(expectedId, actualId);
    }
}
