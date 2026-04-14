using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Cli;

public class CliCommandRunnerTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenLoggerIsProvided()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();

        // Act & Assert
        var exception = Record.Exception(() => new CliCommandRunner(loggerMock.Object));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenCommandSucceeds()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();
        var runner = new CliCommandRunner(loggerMock.Object);

        // Use 'echo' command which should succeed on all platforms
        var command = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "echo";
        var arguments = Environment.OSVersion.Platform == PlatformID.Win32NT 
            ? new[] { "/c", "echo", "test" } 
            : new[] { "test" };

        // Act
        var result = await runner.ExecuteAsync(command, arguments);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.StandardOutput);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenCommandFails()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();
        var runner = new CliCommandRunner(loggerMock.Object);

        // Act - use a command that exists but will fail with the given arguments
        var result = await runner.ExecuteAsync("ls", new[] { "/nonexistent-path-12345" });

        // Assert
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOutput_WhenCommandProducesOutput()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();
        var runner = new CliCommandRunner(loggerMock.Object);

        var command = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "echo";
        var arguments = Environment.OSVersion.Platform == PlatformID.Win32NT 
            ? new[] { "/c", "echo", "hello world" } 
            : new[] { "hello world" };

        // Act
        var result = await runner.ExecuteAsync(command, arguments);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("hello", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenCommandProducesError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();
        var runner = new CliCommandRunner(loggerMock.Object);

        // Use a command that produces error output
        var command = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "ls";
        var arguments = Environment.OSVersion.Platform == PlatformID.Win32NT 
            ? new[] { "/c", "dir", "nonexistent-dir-12345" } 
            : new[] { "/nonexistent-dir-12345" };

        // Act
        var result = await runner.ExecuteAsync(command, arguments);

        // Assert
        Assert.NotNull(result);
        // Command should fail
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellation_WhenCancellationTokenIsCancelled()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();
        var runner = new CliCommandRunner(loggerMock.Object);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = "sleep";
        var arguments = new[] { "10" };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => 
        {
            await runner.ExecuteAsync(command, arguments, cts.Token);
        });
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsExitCode_WhenCommandCompletes()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();
        var runner = new CliCommandRunner(loggerMock.Object);

        var command = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "sh";
        var arguments = Environment.OSVersion.Platform == PlatformID.Win32NT 
            ? new[] { "/c", "exit", "42" } 
            : new[] { "-c", "exit 42" };

        // Act
        var result = await runner.ExecuteAsync(command, arguments);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(42, result.ExitCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesEmptyArguments()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();
        var runner = new CliCommandRunner(loggerMock.Object);

        var command = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "echo";
        var arguments = Array.Empty<string>();

        // Act
        var result = await runner.ExecuteAsync(command, arguments);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesMultipleArguments()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CliCommandRunner>>();
        var runner = new CliCommandRunner(loggerMock.Object);

        var command = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "echo";
        var arguments = Environment.OSVersion.Platform == PlatformID.Win32NT 
            ? new[] { "/c", "echo", "arg1", "arg2", "arg3" } 
            : new[] { "arg1", "arg2", "arg3" };

        // Act
        var result = await runner.ExecuteAsync(command, arguments);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void CommandLineResult_Record_HasCorrectProperties()
    {
        // Arrange
        var result = new CommandLineResult(0, "output", "error", true, CommandErrorCategory.None);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("output", result.StandardOutput);
        Assert.Equal("error", result.StandardError);
        Assert.True(result.Success);
        Assert.Equal(CommandErrorCategory.None, result.ErrorCategory);
    }

    [Fact]
    public void CommandLineResult_Record_EqualityWorks()
    {
        // Arrange
        var result1 = new CommandLineResult(0, "output", "error", true, CommandErrorCategory.None);
        var result2 = new CommandLineResult(0, "output", "error", true, CommandErrorCategory.None);

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
    }

    [Fact]
    public void CommandLineResult_Record_InequalityWorks()
    {
        // Arrange
        var result1 = new CommandLineResult(0, "output", "error", true, CommandErrorCategory.None);
        var result2 = new CommandLineResult(1, "output", "error", false, CommandErrorCategory.RuntimeError);

        // Assert
        Assert.NotEqual(result1, result2);
    }
}
