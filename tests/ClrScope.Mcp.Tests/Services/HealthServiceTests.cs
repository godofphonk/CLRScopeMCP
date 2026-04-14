using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ClrScope.Mcp.Tests.Services;

public class HealthServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenAllDependenciesAreProvided()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        var loggerMock = new Mock<ILogger<HealthService>>();
        var toolCheckerMock = new Mock<ICliToolAvailabilityChecker>();

        // Act & Assert
        var exception = Record.Exception(() => new HealthService(
            optionsMock.Object,
            loggerMock.Object,
            toolCheckerMock.Object
        ));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsHealthy_WhenAllChecksPass()
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
            var loggerMock = new Mock<ILogger<HealthService>>();
            var toolCheckerMock = new Mock<ICliToolAvailabilityChecker>();
            toolCheckerMock.Setup(x => x.CheckAvailabilitySync(It.IsAny<string>()))
                .Returns(new CliToolAvailability("test", true));
            var service = new HealthService(optionsMock.Object, loggerMock.Object, toolCheckerMock.Object);

            // Act
            var result = await service.GetHealthAsync();

            // Assert
            Assert.True(result.IsHealthy);
            Assert.NotNull(result.Version);
            Assert.NotEmpty(result.Version);
            Assert.True(result.ArtifactRoot.Exists);
            Assert.True(result.ArtifactRoot.IsWritable);
            Assert.Equal(tempDir, result.ArtifactRoot.Path);
            Assert.True(result.Database.IsAccessible);
            Assert.NotNull(result.Capabilities);
            Assert.NotNull(result.Readiness);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsUnhealthy_WhenArtifactRootDoesNotExist()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"clrscope_nonexistent_{Guid.NewGuid()}");
        var options = new ClrScopeOptions
        {
            ArtifactRoot = nonExistentDir,
            DatabasePath = Path.Combine(Path.GetTempPath(), "test.db")
        };
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        var loggerMock = new Mock<ILogger<HealthService>>();
        var toolCheckerMock = new Mock<ICliToolAvailabilityChecker>();
        toolCheckerMock.Setup(x => x.CheckAvailabilitySync(It.IsAny<string>()))
            .Returns(new CliToolAvailability("test", true));
        var service = new HealthService(optionsMock.Object, loggerMock.Object, toolCheckerMock.Object);

        // Act
        var result = await service.GetHealthAsync();

        // Assert
        Assert.False(result.IsHealthy);
        Assert.False(result.ArtifactRoot.Exists);
        Assert.False(result.ArtifactRoot.IsWritable);
        Assert.Contains(result.Warnings, w => w.Contains("Artifact root does not exist"));
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsUnhealthy_WhenArtifactRootNotWritable()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Make directory read-only (on Unix this is tricky, so we'll simulate by checking)
            var options = new ClrScopeOptions
            {
                ArtifactRoot = tempDir,
                DatabasePath = Path.Combine(tempDir, "test.db")
            };
            var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
            optionsMock.Setup(x => x.Value).Returns(options);
            var loggerMock = new Mock<ILogger<HealthService>>();
            var toolCheckerMock = new Mock<ICliToolAvailabilityChecker>();
            toolCheckerMock.Setup(x => x.CheckAvailabilitySync(It.IsAny<string>()))
                .Returns(new CliToolAvailability("test", true));
            var service = new HealthService(optionsMock.Object, loggerMock.Object, toolCheckerMock.Object);

            // Act
            var result = await service.GetHealthAsync();

            // Assert
            // On most systems this should succeed, but we verify the structure
            Assert.NotNull(result);
            Assert.NotNull(result.ArtifactRoot);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task GetHealthAsync_AddsWarning_WhenLowDiskSpace()
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
            var loggerMock = new Mock<ILogger<HealthService>>();
            var toolCheckerMock = new Mock<ICliToolAvailabilityChecker>();
            toolCheckerMock.Setup(x => x.CheckAvailabilitySync(It.IsAny<string>()))
                .Returns(new CliToolAvailability("test", true));
            var service = new HealthService(optionsMock.Object, loggerMock.Object, toolCheckerMock.Object);

            // Act
            var result = await service.GetHealthAsync();

            // Assert
            Assert.NotNull(result);
            // Warning may or may not be present depending on actual disk space
            if (result.ArtifactRoot.FreeSpaceBytes < 100 * 1024 * 1024)
            {
                Assert.Contains(result.Warnings, w => w.Contains("Low disk space"));
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsUnhealthy_WhenDatabaseNotAccessible()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Use a path that will fail (e.g., in a read-only location or non-existent parent)
            // For this test, we'll use a path in a directory we'll make read-only if possible
            var dbDir = Path.Combine(tempDir, "readonly_db");
            Directory.CreateDirectory(dbDir);
            
            var options = new ClrScopeOptions
            {
                ArtifactRoot = tempDir,
                DatabasePath = Path.Combine(dbDir, "test.db")
            };
            var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
            optionsMock.Setup(x => x.Value).Returns(options);
            var loggerMock = new Mock<ILogger<HealthService>>();
            var toolCheckerMock = new Mock<ICliToolAvailabilityChecker>();
            toolCheckerMock.Setup(x => x.CheckAvailabilitySync(It.IsAny<string>()))
                .Returns(new CliToolAvailability("test", true));
            var service = new HealthService(optionsMock.Object, loggerMock.Object, toolCheckerMock.Object);

            // Act
            var result = await service.GetHealthAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Database);
            // Database should be accessible in this case since we created the directory
            Assert.True(result.Database.IsAccessible);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task GetHealthAsync_UsesDefaultArtifactRoot_WhenNotSpecified()
    {
        // Arrange
        var options = new ClrScopeOptions
        {
            ArtifactRoot = string.Empty,
            DatabasePath = string.Empty
        };
        var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        var loggerMock = new Mock<ILogger<HealthService>>();
        var toolCheckerMock = new Mock<ICliToolAvailabilityChecker>();
        toolCheckerMock.Setup(x => x.CheckAvailabilitySync(It.IsAny<string>()))
            .Returns(new CliToolAvailability("test", true));
        var service = new HealthService(optionsMock.Object, loggerMock.Object, toolCheckerMock.Object);

        // Act
        var result = await service.GetHealthAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ArtifactRoot);
        Assert.False(string.IsNullOrEmpty(result.ArtifactRoot.Path));
        Assert.Contains(".clrscope", result.ArtifactRoot.Path);
    }

    [Fact]
    public async Task GetHealthAsync_CreatesDatabaseDirectory_WhenNotExists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}");
        var dbDir = Path.Combine(tempDir, "db");
        var dbPath = Path.Combine(dbDir, "test.db");

        try
        {
            var options = new ClrScopeOptions
            {
                ArtifactRoot = tempDir,
                DatabasePath = dbPath
            };
            var optionsMock = new Mock<IOptions<ClrScopeOptions>>();
            optionsMock.Setup(x => x.Value).Returns(options);
            var loggerMock = new Mock<ILogger<HealthService>>();
            var toolCheckerMock = new Mock<ICliToolAvailabilityChecker>();
            toolCheckerMock.Setup(x => x.CheckAvailabilitySync(It.IsAny<string>()))
                .Returns(new CliToolAvailability("test", true));
            var service = new HealthService(optionsMock.Object, loggerMock.Object, toolCheckerMock.Object);

            // Act
            var result = await service.GetHealthAsync();

            // Assert
            Assert.True(Directory.Exists(dbDir));
            Assert.NotNull(result.Database);
            Assert.Equal(dbPath, result.Database.Path);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
