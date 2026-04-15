using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain.Artifacts;

public class ArtifactTests
{
    [Fact]
    public void Artifact_CreatesWithValidParameters()
    {
        // Arrange
        var artifactId = new ArtifactId("test-id");
        var kind = ArtifactKind.Dump;
        var status = ArtifactStatus.Completed;
        var filePath = "/path/to/file.dmp";
        var pid = 1234;
        var sessionId = new SessionId("session-id");
        var createdAtUtc = DateTime.UtcNow;

        // Act
        var artifact = new Artifact(
            artifactId,
            kind,
            status,
            filePath,
            null, // DiagUri
            null, // FileUri
            null, // Sha256
            HashState.Computed,
            1024, // SizeBytes
            pid,
            sessionId,
            createdAtUtc
        );

        // Assert
        Assert.Equal(artifactId, artifact.ArtifactId);
        Assert.Equal(kind, artifact.Kind);
        Assert.Equal(status, artifact.Status);
        Assert.Equal(filePath, artifact.FilePath);
        Assert.Null(artifact.DiagUri);
        Assert.Null(artifact.FileUri);
        Assert.Null(artifact.Sha256);
        Assert.Equal(HashState.Computed, artifact.HashState);
        Assert.Equal(1024, artifact.SizeBytes);
        Assert.Equal(pid, artifact.Pid);
        Assert.Equal(sessionId, artifact.SessionId);
        Assert.Equal(createdAtUtc, artifact.CreatedAtUtc);
        Assert.False(artifact.Pinned);
    }

    [Fact]
    public void Artifact_CreatesWithPinnedParameter()
    {
        // Arrange
        var artifactId = new ArtifactId("test-id");
        var kind = ArtifactKind.Dump;
        var status = ArtifactStatus.Completed;
        var filePath = "/path/to/file.dmp";
        var pid = 1234;
        var sessionId = new SessionId("session-id");
        var createdAtUtc = DateTime.UtcNow;

        // Act
        var artifact = new Artifact(
            artifactId,
            kind,
            status,
            filePath,
            null, // DiagUri
            null, // FileUri
            null, // Sha256
            HashState.Computed,
            1024, // SizeBytes
            pid,
            sessionId,
            createdAtUtc,
            true // Pinned
        );

        // Assert
        Assert.True(artifact.Pinned);
    }

    [Fact]
    public void Artifact_Sha256_CanBeNull()
    {
        // Arrange & Act
        var artifact = new Artifact(
            new ArtifactId("test-id"),
            ArtifactKind.Dump,
            ArtifactStatus.Completed,
            "/path/to/file.dmp",
            null,
            null,
            null, // Sha256
            HashState.SkippedLargeFile,
            1024,
            1234,
            new SessionId("session-id"),
            DateTime.UtcNow
        );

        // Assert
        Assert.Null(artifact.Sha256);
    }

    [Fact]
    public void Artifact_HashState_CanBeComputed()
    {
        // Arrange & Act
        var artifact = new Artifact(
            new ArtifactId("test-id"),
            ArtifactKind.Dump,
            ArtifactStatus.Completed,
            "/path/to/file.dmp",
            null,
            null,
            "abc123",
            HashState.Computed,
            1024,
            1234,
            new SessionId("session-id"),
            DateTime.UtcNow
        );

        // Assert
        Assert.Equal(HashState.Computed, artifact.HashState);
        Assert.Equal("abc123", artifact.Sha256);
    }

    [Fact]
    public void Artifact_HashState_CanBeSkippedLargeFile()
    {
        // Arrange & Act
        var artifact = new Artifact(
            new ArtifactId("test-id"),
            ArtifactKind.Dump,
            ArtifactStatus.Completed,
            "/path/to/file.dmp",
            null,
            null,
            null,
            HashState.SkippedLargeFile,
            1024,
            1234,
            new SessionId("session-id"),
            DateTime.UtcNow
        );

        // Assert
        Assert.Equal(HashState.SkippedLargeFile, artifact.HashState);
    }

    [Fact]
    public void Artifact_HashState_CanBeFailed()
    {
        // Arrange & Act
        var artifact = new Artifact(
            new ArtifactId("test-id"),
            ArtifactKind.Dump,
            ArtifactStatus.Completed,
            "/path/to/file.dmp",
            null,
            null,
            null,
            HashState.Failed,
            1024,
            1234,
            new SessionId("session-id"),
            DateTime.UtcNow
        );

        // Assert
        Assert.Equal(HashState.Failed, artifact.HashState);
    }
}
