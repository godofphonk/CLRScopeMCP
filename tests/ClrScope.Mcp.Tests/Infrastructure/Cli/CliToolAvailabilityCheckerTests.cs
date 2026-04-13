using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Cli;

public class CliToolAvailabilityCheckerTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var loggerMock = new Mock<ILogger<CliToolAvailabilityChecker>>();

        // Act & Assert
        var exception = Record.Exception(() => new CliToolAvailabilityChecker(
            cliRunnerMock.Object,
            loggerMock.Object
        ));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsNotAvailable_WhenToolNotFound()
    {
        // Arrange
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(0, "", "", true));
        cliRunnerMock.Setup(x => x.ExecuteAsync("which", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(1, "", "not found", false));

        var loggerMock = new Mock<ILogger<CliToolAvailabilityChecker>>();
        var checker = new CliToolAvailabilityChecker(cliRunnerMock.Object, loggerMock.Object);

        // Act
        var result = await checker.CheckAvailabilityAsync("nonexistent-tool", CancellationToken.None);

        // Assert
        Assert.False(result.IsAvailable);
        Assert.Equal("nonexistent-tool", result.ToolName);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_UsesCache_WhenCalledTwice()
    {
        // Arrange
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        cliRunnerMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(0, "dotnet-dump 9.0.0", "", true));

        var loggerMock = new Mock<ILogger<CliToolAvailabilityChecker>>();
        var checker = new CliToolAvailabilityChecker(cliRunnerMock.Object, loggerMock.Object);

        // Act
        var result1 = await checker.CheckAvailabilityAsync("dotnet-dump", CancellationToken.None);
        var result2 = await checker.CheckAvailabilityAsync("dotnet-dump", CancellationToken.None);

        // Assert
        Assert.True(result1.IsAvailable);
        Assert.True(result2.IsAvailable);
        // Should only call ExecuteAsync once due to caching
        cliRunnerMock.Verify(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once());
    }
}
