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

namespace ClrScope.Mcp.Tests.Services;

public class CollectDumpServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        var sessionStoreMock = new Mock<ISqliteSessionStore>();
        var artifactStoreMock = new Mock<ISqliteArtifactStore>();
        var preflightValidatorMock = new Mock<IPreflightValidator>();
        var pidLockManagerMock = new Mock<IPidLockManager>();
        var activeOperationRegistryMock = new Mock<IActiveOperationRegistry>();
        var loggerMock = new Mock<ILogger<CollectDumpService>>();

        // Act & Assert
        var exception = Record.Exception(() => new CollectDumpService(
            optionsMock.Object,
            sessionStoreMock.Object,
            artifactStoreMock.Object,
            preflightValidatorMock.Object,
            pidLockManagerMock.Object,
            activeOperationRegistryMock.Object,
            loggerMock.Object
        ));

        Assert.Null(exception);
    }
}
