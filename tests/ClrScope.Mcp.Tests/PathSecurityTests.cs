using ClrScope.Mcp.Infrastructure.Utils;
using Xunit;

namespace ClrScope.Mcp.Tests;

public class PathSecurityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _trustedDir;

    public PathSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        
        _trustedDir = Path.Combine(_tempDir, "trusted");
        Directory.CreateDirectory(_trustedDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void IsPathWithinDirectory_AllowsValidPath()
    {
        // Arrange
        var validFile = Path.Combine(_trustedDir, "file.txt");
        File.WriteAllText(validFile, "test");

        // Act
        var result = PathSecurity.IsPathWithinDirectory(validFile, _trustedDir);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPathWithinDirectory_AllowsNestedPath()
    {
        // Arrange
        var nestedDir = Path.Combine(_trustedDir, "nested");
        Directory.CreateDirectory(nestedDir);
        var nestedFile = Path.Combine(nestedDir, "file.txt");
        File.WriteAllText(nestedFile, "test");

        // Act
        var result = PathSecurity.IsPathWithinDirectory(nestedFile, _trustedDir);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsSiblingPrefixBypass()
    {
        // Arrange
        var evilDir = Path.Combine(_tempDir, "trusted_evil");
        Directory.CreateDirectory(evilDir);
        var evilFile = Path.Combine(evilDir, "file.txt");
        File.WriteAllText(evilFile, "test");

        // Act
        var result = PathSecurity.IsPathWithinDirectory(evilFile, _trustedDir);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsRelativeTraversal()
    {
        // Arrange
        var outsideDir = Path.Combine(_tempDir, "outside");
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "file.txt");
        File.WriteAllText(outsideFile, "test");

        // Act - try to use relative path to escape
        var relativePath = Path.Combine(_trustedDir, "..", "outside", "file.txt");
        var result = PathSecurity.IsPathWithinDirectory(relativePath, _trustedDir);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsAbsolutePathOutside()
    {
        // Arrange
        var outsideDir = Path.Combine(_tempDir, "outside");
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "file.txt");
        File.WriteAllText(outsideFile, "test");

        // Act
        var result = PathSecurity.IsPathWithinDirectory(outsideFile, _trustedDir);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_HandlesMixedSeparators()
    {
        // Arrange
        var validFile = Path.Combine(_trustedDir, "file.txt");
        File.WriteAllText(validFile, "test");

        // Act - use mixed separators (forward and backward)
        var mixedPath = _trustedDir.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + 
                        Path.AltDirectorySeparatorChar + "file.txt";
        var result = PathSecurity.IsPathWithinDirectory(mixedPath, _trustedDir);

        // Assert - GetFullPath normalizes separators, so this should pass
        Assert.True(result);
    }

    [Fact]
    public void IsPathWithinDirectory_CaseSensitiveOnLinux()
    {
        // Arrange
        var validFile = Path.Combine(_trustedDir, "file.txt");
        File.WriteAllText(validFile, "test");

        // Act - use different case on Linux
        var caseChangedPath = _trustedDir.ToUpper();
        var result = PathSecurity.IsPathWithinDirectory(caseChangedPath, _trustedDir);

        // Assert - On Linux (case-sensitive), this should fail
        // On Windows (case-insensitive), this should pass
        var isCaseSensitive = Environment.OSVersion.Platform == PlatformID.Unix;
        Assert.Equal(!isCaseSensitive, result);
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsSymlinkOutofRoot()
    {
        // Skip on Windows where symlinks require elevated privileges
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return;
        }

        // Arrange
        var outsideDir = Path.Combine(_tempDir, "outside");
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "file.txt");
        File.WriteAllText(outsideFile, "test");

        var symlinkPath = Path.Combine(_trustedDir, "symlink");
        try
        {
            File.CreateSymbolicLink(symlinkPath, outsideFile);
        }
        catch (UnauthorizedAccessException)
        {
            // Symlink creation requires privileges, skip test
            return;
        }

        // Act
        var result = PathSecurity.IsPathWithinDirectory(symlinkPath, _trustedDir);

        // Assert - Should reject because symlink points outside trusted directory
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_AllowsSymlinkWithinRoot()
    {
        // Skip on Windows where symlinks require elevated privileges
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return;
        }

        // Arrange
        var targetFile = Path.Combine(_trustedDir, "target.txt");
        File.WriteAllText(targetFile, "test");

        var symlinkPath = Path.Combine(_trustedDir, "symlink");
        try
        {
            File.CreateSymbolicLink(symlinkPath, targetFile);
        }
        catch (UnauthorizedAccessException)
        {
            // Symlink creation requires privileges, skip test
            return;
        }

        // Act
        var result = PathSecurity.IsPathWithinDirectory(symlinkPath, _trustedDir);

        // Assert - Should allow because symlink points within trusted directory
        Assert.True(result);
    }

    [Fact]
    public void EnsurePathWithinDirectory_ThrowsWhenPathOutside()
    {
        // Arrange
        var outsideFile = Path.Combine(_tempDir, "outside.txt");

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() =>
            PathSecurity.EnsurePathWithinDirectory(outsideFile, _trustedDir));
    }

    [Fact]
    public void EnsurePathWithinDirectory_DoesNotThrowWhenPathInside()
    {
        // Arrange
        var validFile = Path.Combine(_trustedDir, "file.txt");
        File.WriteAllText(validFile, "test");

        // Act & Assert - should not throw
        PathSecurity.EnsurePathWithinDirectory(validFile, _trustedDir);
    }

    [Fact]
    public void IsPathWithinDirectory_HandlesNonExistentPath()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_trustedDir, "nonexistent.txt");

        // Act
        var result = PathSecurity.IsPathWithinDirectory(nonExistentFile, _trustedDir);

        // Assert - Should still validate the path structure even if file doesn't exist
        Assert.True(result);
    }

    [Fact]
    public void IsPathWithinDirectory_HandlesInvalidPath()
    {
        // Arrange
        var invalidPath = "\0\0\0"; // Null characters are invalid

        // Act
        var result = PathSecurity.IsPathWithinDirectory(invalidPath, _trustedDir);

        // Assert - Should return false for invalid paths
        Assert.False(result);
    }
}
