using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services;
using ClrScope.Mcp.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests;

public class CollectStacksServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        var preflightValidatorMock = new Mock<IPreflightValidator>();
        var sessionStoreMock = new Mock<ISqliteSessionStore>();
        var artifactStoreMock = new Mock<ISqliteArtifactStore>();
        var pidLockManagerMock = new Mock<IPidLockManager>();
        var activeOperationRegistryMock = new Mock<IActiveOperationRegistry>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();
        var loggerMock = new Mock<ILogger<CollectStacksService>>();

        // Act & Assert
        var exception = Record.Exception(() => new CollectStacksService(
            optionsMock.Object,
            preflightValidatorMock.Object,
            sessionStoreMock.Object,
            artifactStoreMock.Object,
            pidLockManagerMock.Object,
            activeOperationRegistryMock.Object,
            cliRunnerMock.Object,
            availabilityCheckerMock.Object,
            loggerMock.Object
        ));

        Assert.Null(exception);
    }
}
