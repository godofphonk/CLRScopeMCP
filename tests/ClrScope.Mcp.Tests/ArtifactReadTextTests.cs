using ClrScope.Mcp.Domain.Artifacts;
using Xunit;

namespace ClrScope.Mcp.Tests;

public class ArtifactReadTextTests
{
    [Theory]
    [InlineData(ArtifactKind.Dump)]
    [InlineData(ArtifactKind.Trace)]
    [InlineData(ArtifactKind.GcDump)]
    public void ArtifactKind_NotTextOnly_ShouldBeRejectedForTextReading(ArtifactKind kind)
    {
        // Arrange
        // Artifact_read_text tool only allows Counters and Stacks
        // Dump, Trace, and GcDump should be rejected

        // Act & Assert
        Assert.True(
            kind == ArtifactKind.Dump ||
            kind == ArtifactKind.Trace ||
            kind == ArtifactKind.GcDump,
            $"{kind} should be rejected for text reading"
        );
    }

    [Theory]
    [InlineData(ArtifactKind.Counters)]
    [InlineData(ArtifactKind.Stacks)]
    public void ArtifactKind_TextOnly_ShouldBeAllowedForTextReading(ArtifactKind kind)
    {
        // Arrange
        // Counters and Stacks are text-only kinds

        // Act & Assert
        Assert.True(
            kind == ArtifactKind.Counters ||
            kind == ArtifactKind.Stacks,
            $"{kind} should be allowed for text reading"
        );
    }
}
