using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Diagnostics;

public class DotnetDumpAnalyzerTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        // Act & Assert
        var exception = Record.Exception(() => new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        ));

        Assert.Null(exception);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_WhenDotnetDumpIsAvailable()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        availabilityCheckerMock.Setup(x => x.CheckAvailabilityAsync("dotnet-dump", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliToolAvailability("dotnet-dump", true));
        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-dump", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(0, "help output", string.Empty, true));

        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        // Act
        var result = await analyzer.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenDotnetDumpNotAvailable()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        availabilityCheckerMock.Setup(x => x.CheckAvailabilityAsync("dotnet-dump", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliToolAvailability("dotnet-dump", false, null, "not found"));

        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        // Act
        var result = await analyzer.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenDotnetDumpHelpFails()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        availabilityCheckerMock.Setup(x => x.CheckAvailabilityAsync("dotnet-dump", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliToolAvailability("dotnet-dump", true));
        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-dump", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(1, string.Empty, "error", false));

        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        // Act
        var result = await analyzer.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenExceptionThrown()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        availabilityCheckerMock.Setup(x => x.CheckAvailabilityAsync("dotnet-dump", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("test error"));

        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        // Act
        var result = await analyzer.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsSuccess_WhenCommandSucceeds()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-dump", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(0, "sos output", string.Empty, true));

        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var result = await analyzer.ExecuteCommandAsync(tempFile, "dumpheap -stat");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("sos output", result.Output);
            Assert.Null(result.Error);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsFailure_WhenDumpFileNotFound()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();


        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var nonExistentFile = "/nonexistent/dump-file-12345.dmp";

        // Act
        var result = await analyzer.ExecuteCommandAsync(nonExistentFile, "dumpheap -stat");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error.ToLower());
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsFailure_WhenCommandFails()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-dump", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandLineResult(1, string.Empty, "sos error", false));

        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var result = await analyzer.ExecuteCommandAsync(tempFile, "dumpheap -stat");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("sos error", result.Error);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsTimeoutError_WhenCommandTimesOut()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-dump", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cmd, string[] args, CancellationToken ct) =>
            {
                // Simulate timeout by cancelling the token after a delay
                Task.Delay(TimeSpan.FromSeconds(35), ct).Wait(ct);
                return new CommandLineResult(0, "output", string.Empty, true);
            });

        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var result = await analyzer.ExecuteCommandAsync(tempFile, "dumpheap -stat");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("timed out", result.Error.ToLower());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsFailure_WhenExceptionThrown()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DotnetDumpAnalyzer>>();
        var cliRunnerMock = new Mock<ICliCommandRunner>();
        var correlationIdProvider = new CorrelationIdProvider();
        var availabilityCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        cliRunnerMock.Setup(x => x.ExecuteAsync("dotnet-dump", It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("test error"));

        var analyzer = new DotnetDumpAnalyzer(
            loggerMock.Object,
            cliRunnerMock.Object,
            correlationIdProvider,
            availabilityCheckerMock.Object
        );

        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var result = await analyzer.ExecuteCommandAsync(tempFile, "dumpheap -stat");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("test error", result.Error);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void SosAnalysisResult_SuccessResult_CreatesSuccessfulResult()
    {
        // Arrange
        var output = "sos command output";

        // Act
        var result = SosAnalysisResult.SuccessResult(output);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(output, result.Output);
        Assert.Null(result.Error);
    }

    [Fact]
    public void SosAnalysisResult_FailureResult_CreatesFailedResult()
    {
        // Arrange
        var error = "sos command failed";

        // Act
        var result = SosAnalysisResult.FailureResult(error);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void SosAnalysisResult_Record_EqualityWorks()
    {
        // Arrange
        var result1 = SosAnalysisResult.SuccessResult("output");
        var result2 = SosAnalysisResult.SuccessResult("output");

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
    }

    [Fact]
    public void SosAnalysisResult_Record_InequalityWorks()
    {
        // Arrange
        var result1 = SosAnalysisResult.SuccessResult("output");
        var result2 = SosAnalysisResult.FailureResult("error");

        // Assert
        Assert.NotEqual(result1, result2);
    }
}
