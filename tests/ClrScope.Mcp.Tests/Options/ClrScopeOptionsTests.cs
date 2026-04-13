using ClrScope.Mcp.Options;
using Xunit;

namespace ClrScope.Mcp.Tests.Options;

public class ClrScopeOptionsTests
{
    [Fact]
    public void GetArtifactRoot_ReturnsFullPath_WhenArtifactRootIsSet()
    {
        // Arrange
        var options = new ClrScopeOptions
        {
            ArtifactRoot = "/tmp/test"
        };

        // Act
        var result = options.GetArtifactRoot();

        // Assert
        Assert.Equal(Path.GetFullPath("/tmp/test"), result);
    }

    [Fact]
    public void GetArtifactRoot_ReturnsDefaultPath_WhenArtifactRootIsEmpty()
    {
        // Arrange
        var options = new ClrScopeOptions
        {
            ArtifactRoot = string.Empty
        };

        // Act
        var result = options.GetArtifactRoot();

        // Assert
        var expectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clrscope");
        Assert.Equal(Path.GetFullPath(expectedPath), result);
    }

    [Fact]
    public void GetDatabasePath_ReturnsFullPath_WhenDatabasePathIsSet()
    {
        // Arrange
        var options = new ClrScopeOptions
        {
            DatabasePath = "/tmp/test.db"
        };

        // Act
        var result = options.GetDatabasePath();

        // Assert
        Assert.Equal(Path.GetFullPath("/tmp/test.db"), result);
    }

    [Fact]
    public void GetDatabasePath_ReturnsArtifactRootPath_WhenDatabasePathIsEmpty()
    {
        // Arrange
        var options = new ClrScopeOptions
        {
            ArtifactRoot = "/tmp/test",
            DatabasePath = string.Empty
        };

        // Act
        var result = options.GetDatabasePath();

        // Assert
        var expectedPath = Path.Combine(Path.GetFullPath("/tmp/test"), "clrscope.db");
        Assert.Equal(Path.GetFullPath(expectedPath), result);
    }
}
