using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Backends;

public class CliCountersBackendTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();
        var loggerMock = new Mock<ILogger<CliCountersBackend>>();

        // Act & Assert
        var exception = Record.Exception(() => new CliCountersBackend(
            cliRunnerMock.Object,
            availabilityCheckerMock.Object,
            loggerMock.Object
        ));

        Assert.Null(exception);
    }
}
