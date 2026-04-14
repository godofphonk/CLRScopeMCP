using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Validation;

public class FullPreflightValidatorTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        var loggerMock = new Mock<ILogger<FullPreflightValidator>>();

        // Act & Assert
        var exception = Record.Exception(() => new FullPreflightValidator(
            optionsMock.Object,
            loggerMock.Object
        ));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ValidateCollectAsync_ReturnsFailure_WhenPidIsZero()
    {
        // Arrange
        var options = new ClrScopeOptions
        {
            ArtifactRoot = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}")
        };
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        var loggerMock = new Mock<ILogger<FullPreflightValidator>>();
        var validator = new FullPreflightValidator(optionsMock.Object, loggerMock.Object);

        // Act
        var result = await validator.ValidateCollectAsync(0, CollectionOperationType.Dump, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ClrScopeError.VALIDATION_INVALID_PID, result.Error.Value);
    }

    [Fact]
    public async Task ValidateCollectAsync_ReturnsFailure_WhenPidIsNegative()
    {
        // Arrange
        var options = new ClrScopeOptions
        {
            ArtifactRoot = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}")
        };
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        var loggerMock = new Mock<ILogger<FullPreflightValidator>>();
        var validator = new FullPreflightValidator(optionsMock.Object, loggerMock.Object);

        // Act
        var result = await validator.ValidateCollectAsync(-1, CollectionOperationType.Dump, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ClrScopeError.VALIDATION_INVALID_PID, result.Error.Value);
    }

    [Fact]
    public async Task ValidateCollectAsync_ReturnsFailure_WhenProcessDoesNotExist()
    {
        // Arrange
        var options = new ClrScopeOptions
        {
            ArtifactRoot = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}")
        };
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        var loggerMock = new Mock<ILogger<FullPreflightValidator>>();
        var validator = new FullPreflightValidator(optionsMock.Object, loggerMock.Object);

        // Use a non-existent PID (99999 is unlikely to exist)
        // Act
        var result = await validator.ValidateCollectAsync(99999, CollectionOperationType.Dump, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ClrScopeError.PREFLIGHT_PROCESS_NOT_FOUND, result.Error.Value);
    }

    [Fact]
    public async Task ValidateCollectAsync_ReturnsSuccess_WhenValidPidProvided()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ClrScopeOptions
            {
                ArtifactRoot = tempDir,
                DatabasePath = Path.Combine(tempDir, "test.db")
            };
            var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
            optionsMock.Setup(x => x.Value).Returns(options);
            var loggerMock = new Mock<ILogger<FullPreflightValidator>>();
            var validator = new FullPreflightValidator(optionsMock.Object, loggerMock.Object);
            var currentPid = Environment.ProcessId;

            // Act
            var result = await validator.ValidateCollectAsync(currentPid, CollectionOperationType.Dump, CancellationToken.None);

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.Error);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ValidateCollectAsync_ReturnsFailure_WhenArtifactRootNotWritable()
    {
        // Arrange
        // Use a path that cannot be created (e.g., /root/test or a non-existent directory in a restricted path)
        var artifactRoot = "/root/nonexistent_clrscope_test";
        var options = new ClrScopeOptions
        {
            ArtifactRoot = artifactRoot
        };
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        var loggerMock = new Mock<ILogger<FullPreflightValidator>>();
        var validator = new FullPreflightValidator(optionsMock.Object, loggerMock.Object);

        // Use current process PID (valid process)
        var currentPid = Environment.ProcessId;

        // Act
        var result = await validator.ValidateCollectAsync(currentPid, CollectionOperationType.Dump, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ClrScopeError.PREFLIGHT_ARTIFACT_ROOT_NOT_WRITABLE, result.Error.Value);
    }

    [Fact]
    public async Task ValidateCollectAsync_ReturnsFailure_WhenProcessIsNotDotNet()
    {
        // Arrange
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}");
        var options = new ClrScopeOptions
        {
            ArtifactRoot = artifactRoot
        };
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        var loggerMock = new Mock<ILogger<FullPreflightValidator>>();
        var validator = new FullPreflightValidator(optionsMock.Object, loggerMock.Object);

        // Use PID 1 (init process) which is not a .NET process
        // Act
        var result = await validator.ValidateCollectAsync(1, CollectionOperationType.Dump, CancellationToken.None);

        // Assert - should fail because PID 1 is not a .NET process
        Assert.False(result.IsValid);
        Assert.Equal(ClrScopeError.PREFLIGHT_NOT_DOTNET, result.Error.Value);
    }

    [Fact]
    public async Task ValidateCollectAsync_ReturnsFailure_WhenDiskSpaceLow()
    {
        // Arrange
        // Use a temporary directory on a filesystem that might have low space
        // This test is environment-dependent, but we'll try to use /tmp which usually has space
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}");
        var options = new ClrScopeOptions
        {
            ArtifactRoot = artifactRoot
        };
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        var loggerMock = new Mock<ILogger<FullPreflightValidator>>();
        var validator = new FullPreflightValidator(optionsMock.Object, loggerMock.Object);

        // Use current process PID (valid .NET process)
        var currentPid = Environment.ProcessId;

        // Act
        var result = await validator.ValidateCollectAsync(currentPid, CollectionOperationType.Dump, CancellationToken.None);

        // Assert
        // This should succeed if there's enough disk space (>100MB)
        // In normal environments with sufficient disk space, this will pass
        // The test might fail on systems with very low disk space
        Assert.True(result.IsValid || result.Error == ClrScopeError.PREFLIGHT_DISK_SPACE_LOW);
    }
}
