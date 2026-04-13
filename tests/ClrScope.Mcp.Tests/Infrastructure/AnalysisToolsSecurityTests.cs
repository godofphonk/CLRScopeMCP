using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Tools.Analysis;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure;

public class AnalysisToolsSecurityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _artifactRoot;

    public AnalysisToolsSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"clrscope_security_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        
        _artifactRoot = Path.Combine(_tempDir, "artifacts");
        Directory.CreateDirectory(_artifactRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ArtifactPathValidation_RejectsPathOutsideArtifactRoot()
    {
        // Arrange
        var outsideFile = Path.Combine(_tempDir, "outside", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outsideFile)!);
        File.WriteAllText(outsideFile, "test");

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() =>
            PathSecurity.EnsurePathWithinDirectory(outsideFile, _artifactRoot));
    }

    [Fact]
    public void ArtifactPathValidation_AcceptsPathInsideArtifactRoot()
    {
        // Arrange
        var insideFile = Path.Combine(_artifactRoot, "file.txt");
        File.WriteAllText(insideFile, "test");

        // Act & Assert - should not throw
        PathSecurity.EnsurePathWithinDirectory(insideFile, _artifactRoot);
    }

    [Fact]
    public void ArtifactPathValidation_RejectsRelativeTraversal()
    {
        // Arrange
        var outsideDir = Path.Combine(_tempDir, "outside");
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "file.txt");
        File.WriteAllText(outsideFile, "test");

        // Act - try to use relative path to escape
        var relativePath = Path.Combine(_artifactRoot, "..", "outside", "file.txt");

        // Assert
        Assert.Throws<UnauthorizedAccessException>(() =>
            PathSecurity.EnsurePathWithinDirectory(relativePath, _artifactRoot));
    }

    [Fact]
    public void ArtifactPathValidation_RejectsAbsolutePathOutside()
    {
        // Arrange
        var outsideFile = Path.Combine(_tempDir, "outside.txt");
        File.WriteAllText(outsideFile, "test");

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() =>
            PathSecurity.EnsurePathWithinDirectory(outsideFile, _artifactRoot));
    }

    [Fact]
    public void ArtifactPathValidation_RejectsSymlinkOutofRoot()
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

        var symlinkPath = Path.Combine(_artifactRoot, "symlink");
        try
        {
            File.CreateSymbolicLink(symlinkPath, outsideFile);
        }
        catch (UnauthorizedAccessException)
        {
            // Symlink creation requires privileges, skip test
            return;
        }

        // Act & Assert - Should reject because symlink points outside trusted directory
        Assert.Throws<UnauthorizedAccessException>(() =>
            PathSecurity.EnsurePathWithinDirectory(symlinkPath, _artifactRoot));
    }

    [Fact]
    public void ArtifactPathValidation_AllowsSymlinkWithinRoot()
    {
        // Skip on Windows where symlinks require elevated privileges
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return;
        }

        // Arrange
        var targetFile = Path.Combine(_artifactRoot, "target.txt");
        File.WriteAllText(targetFile, "test");

        var symlinkPath = Path.Combine(_artifactRoot, "symlink");
        try
        {
            File.CreateSymbolicLink(symlinkPath, targetFile);
        }
        catch (UnauthorizedAccessException)
        {
            // Symlink creation requires privileges, skip test
            return;
        }

        // Act & Assert - Should allow because symlink points within trusted directory
        PathSecurity.EnsurePathWithinDirectory(symlinkPath, _artifactRoot);
    }

    [Fact]
    public void ArtifactPathValidation_RejectsSiblingPrefixBypass()
    {
        // Arrange
        var evilDir = Path.Combine(_tempDir, "artifacts_evil");
        Directory.CreateDirectory(evilDir);
        var evilFile = Path.Combine(evilDir, "file.txt");
        File.WriteAllText(evilFile, "test");

        // Act & Assert - Should reject because artifacts_evil is not artifacts
        Assert.Throws<UnauthorizedAccessException>(() =>
            PathSecurity.EnsurePathWithinDirectory(evilFile, _artifactRoot));
    }

    [Fact]
    public void ArtifactPathValidation_RejectsCaseSensitivityBypassOnLinux()
    {
        // Arrange
        var validFile = Path.Combine(_artifactRoot, "file.txt");
        File.WriteAllText(validFile, "test");

        // Act - use different case on Linux
        var caseChangedPath = _artifactRoot.ToUpper();
        var caseChangedFile = Path.Combine(caseChangedPath, "file.txt");

        // Assert - On Linux (case-sensitive), this should fail
        // On Windows (case-insensitive), this might pass
        var isCaseSensitive = Environment.OSVersion.Platform == PlatformID.Unix;
        
        if (isCaseSensitive)
        {
            Assert.Throws<UnauthorizedAccessException>(() =>
                PathSecurity.EnsurePathWithinDirectory(caseChangedFile, _artifactRoot));
        }
        else
        {
            // On Windows, case-insensitive, so this should pass
            PathSecurity.EnsurePathWithinDirectory(caseChangedFile, _artifactRoot);
        }
    }

    [Fact]
    public void ArtifactPathValidation_AllowsNestedPaths()
    {
        // Arrange
        var nestedDir = Path.Combine(_artifactRoot, "nested", "deep");
        Directory.CreateDirectory(nestedDir);
        var nestedFile = Path.Combine(nestedDir, "file.txt");
        File.WriteAllText(nestedFile, "test");

        // Act & Assert - should not throw
        PathSecurity.EnsurePathWithinDirectory(nestedFile, _artifactRoot);
    }

    [Fact]
    public void ArtifactPathValidation_RejectsInvalidPath()
    {
        // Arrange
        var invalidPath = "\0\0\0"; // Null characters are invalid

        // Act & Assert - Should throw for invalid paths
        Assert.Throws<UnauthorizedAccessException>(() =>
            PathSecurity.EnsurePathWithinDirectory(invalidPath, _artifactRoot));
    }

    [Fact]
    public void ArtifactPathValidation_RejectsNonExistentPathOutside()
    {
        // Arrange
        var nonExistentOutside = Path.Combine(_tempDir, "nonexistent.txt");

        // Act & Assert - Should reject even if file doesn't exist
        Assert.Throws<UnauthorizedAccessException>(() =>
            PathSecurity.EnsurePathWithinDirectory(nonExistentOutside, _artifactRoot));
    }

    [Fact]
    public void ArtifactPathValidation_AllowsNonExistentPathInside()
    {
        // Arrange
        var nonExistentInside = Path.Combine(_artifactRoot, "nonexistent.txt");

        // Act & Assert - Should allow because path structure is valid, even if file doesn't exist
        PathSecurity.EnsurePathWithinDirectory(nonExistentInside, _artifactRoot);
    }
}
