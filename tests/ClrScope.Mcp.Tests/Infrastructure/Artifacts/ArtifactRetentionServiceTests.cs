using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Artifacts;

public class ArtifactRetentionServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var artifactStoreMock = new Mock<ISqliteArtifactStore>();
        var loggerMock = new Mock<ILogger<ArtifactRetentionService>>();
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        var options = new ClrScopeOptions { ArtifactRoot = "/tmp/test" };
        optionsMock.Setup(x => x.Value).Returns(options);

        // Act & Assert
        var exception = Record.Exception(() => new ArtifactRetentionService(
            artifactStoreMock.Object,
            loggerMock.Object,
            optionsMock.Object
        ));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetTotalArtifactSizeAsync_ReturnsZero_WhenNoArtifacts()
    {
        // Arrange
        var artifactStoreMock = new Mock<ISqliteArtifactStore>();
        artifactStoreMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClrScope.Mcp.Domain.Artifacts.Artifact>());
        var loggerMock = new Mock<ILogger<ArtifactRetentionService>>();
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        var options = new ClrScopeOptions { ArtifactRoot = "/tmp/test" };
        optionsMock.Setup(x => x.Value).Returns(options);
        var service = new ArtifactRetentionService(artifactStoreMock.Object, loggerMock.Object, optionsMock.Object);

        // Act
        var totalSize = await service.GetTotalArtifactSizeAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, totalSize);
    }
}
