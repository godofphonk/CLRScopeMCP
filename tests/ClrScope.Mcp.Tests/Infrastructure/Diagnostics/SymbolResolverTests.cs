using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Diagnostics;

public class SymbolResolverTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        // Act & Assert
        var exception = Record.Exception(() => new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        ));

        Assert.Null(exception);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_WhenDotnetSymbolIsAvailable()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        availabilityCheckerMock.Setup(x => x.CheckAvailabilityAsync("dotnet-symbol", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliToolAvailability("dotnet-symbol", true));
        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-symbol", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(0, "help output", string.Empty, true));

        var resolver = new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        // Act
        var result = await resolver.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenDotnetSymbolNotAvailable()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        availabilityCheckerMock.Setup(x => x.CheckAvailabilityAsync("dotnet-symbol", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliToolAvailability("dotnet-symbol", false, null, "not found"));

        var resolver = new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        // Act
        var result = await resolver.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenDotnetSymbolHelpFails()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        availabilityCheckerMock.Setup(x => x.CheckAvailabilityAsync("dotnet-symbol", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliToolAvailability("dotnet-symbol", true));
        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-symbol", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(1, string.Empty, "error", false));

        var resolver = new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        // Act
        var result = await resolver.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenExceptionThrown()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        availabilityCheckerMock.Setup(x => x.CheckAvailabilityAsync("dotnet-symbol", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("test error"));

        var resolver = new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        // Act
        var result = await resolver.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsSuccess_WhenSymbolResolutionSucceeds()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-symbol", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(0, "success", string.Empty, true));

        var resolver = new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var filePath = "/path/to/artifact.dll";

        // Act
        var result = await resolver.ResolveAsync(filePath);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.SymbolPath);
        Assert.Contains("symbols", result.SymbolPath);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsFailure_WhenSymbolResolutionFails()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-symbol", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(1, string.Empty, "symbol error", false));

        var resolver = new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var filePath = "/path/to/artifact.dll";

        // Act
        var result = await resolver.ResolveAsync(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("symbol error", result.Error);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsFailure_WhenExceptionThrown()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-symbol", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("test error"));

        var resolver = new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var filePath = "/path/to/artifact.dll";

        // Act
        var result = await resolver.ResolveAsync(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("test error", result.Error);
    }

    [Fact]
    public async Task ResolveAsync_CreatesSymbolCacheDirectory_WhenNotExists()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SymbolResolver>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-symbol", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(0, "success", string.Empty, true));

        var resolver = new SymbolResolver(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var filePath = "/path/to/artifact.dll";

        // Act
        var result = await resolver.ResolveAsync(filePath);

        // Assert
        Assert.True(result.Success);
        var symbolCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clrscope",
            "symbols"
        );
        Assert.Contains("symbols", result.SymbolPath);
    }

    [Fact]
    public void SymbolResolutionResult_SuccessResult_CreatesSuccessfulResult()
    {
        // Arrange
        var symbolPath = "/path/to/symbols";

        // Act
        var result = SymbolResolutionResult.SuccessResult(symbolPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(symbolPath, result.SymbolPath);
        Assert.Null(result.Error);
    }

    [Fact]
    public void SymbolResolutionResult_FailureResult_CreatesFailedResult()
    {
        // Arrange
        var error = "symbol resolution failed";

        // Act
        var result = SymbolResolutionResult.FailureResult(error);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.SymbolPath);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void SymbolResolutionResult_Record_EqualityWorks()
    {
        // Arrange
        var result1 = SymbolResolutionResult.SuccessResult("/path/to/symbols");
        var result2 = SymbolResolutionResult.SuccessResult("/path/to/symbols");

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
    }

    [Fact]
    public void SymbolResolutionResult_Record_InequalityWorks()
    {
        // Arrange
        var result1 = SymbolResolutionResult.SuccessResult("/path/to/symbols");
        var result2 = SymbolResolutionResult.FailureResult("error");

        // Assert
        Assert.NotEqual(result1, result2);
    }
}
