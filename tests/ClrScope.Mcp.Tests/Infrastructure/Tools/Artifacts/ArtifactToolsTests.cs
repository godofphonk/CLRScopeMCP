using ClrScope.Mcp.Tools.Artifacts;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Tools.Artifacts;

public class ArtifactToolsTests
{
    #region GetArtifactMetadata Tests

    [Fact]
    public async Task GetArtifactMetadata_ReturnsError_WhenArtifactIdIsEmpty()
    {
        // Arrange
        var artifactId = "";

        // Act
        var result = await ArtifactCrudTools.GetArtifactMetadata(artifactId, null!);

        // Assert
        Assert.False(result.Found);
        Assert.Equal("Artifact ID must not be empty", result.Error);
    }

    [Fact]
    public async Task GetArtifactMetadata_ReturnsError_WhenArtifactIdIsWhitespace()
    {
        // Arrange
        var artifactId = "   ";

        // Act
        var result = await ArtifactCrudTools.GetArtifactMetadata(artifactId, null!);

        // Assert
        Assert.False(result.Found);
        Assert.Equal("Artifact ID must not be empty", result.Error);
    }

    [Fact]
    public async Task GetArtifactMetadata_ReturnsError_WhenArtifactIdIsNull()
    {
        // Arrange
        string artifactId = null!;

        // Act
        var result = await ArtifactCrudTools.GetArtifactMetadata(artifactId, null!);

        // Assert
        Assert.False(result.Found);
        Assert.Equal("Artifact ID must not be empty", result.Error);
    }

    #endregion

    #region ListArtifacts Tests

    [Fact]
    public async Task ListArtifacts_ReturnsError_WhenOffsetIsNegative()
    {
        // Arrange
        var offset = -1;
        var limit = 50;

        // Act
        var result = await ArtifactCrudTools.ListArtifacts(null!, offset: offset, limit: limit);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Equal("Offset must be >= 0", result.Error);
    }

    [Fact]
    public async Task ListArtifacts_ReturnsError_WhenLimitIsZero()
    {
        // Arrange
        var offset = 0;
        var limit = 0;

        // Act
        var result = await ArtifactCrudTools.ListArtifacts(null!, offset: offset, limit: limit);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Equal("Limit must be between 1 and 500", result.Error);
    }

    [Fact]
    public async Task ListArtifacts_ReturnsError_WhenLimitIsNegative()
    {
        // Arrange
        var offset = 0;
        var limit = -1;

        // Act
        var result = await ArtifactCrudTools.ListArtifacts(null!, offset: offset, limit: limit);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Equal("Limit must be between 1 and 500", result.Error);
    }

    [Fact]
    public async Task ListArtifacts_ReturnsError_WhenLimitIsGreaterThan500()
    {
        // Arrange
        var offset = 0;
        var limit = 501;

        // Act
        var result = await ArtifactCrudTools.ListArtifacts(null!, offset: offset, limit: limit);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Equal("Limit must be between 1 and 500", result.Error);
    }

    [Fact]
    public async Task ListArtifacts_ReturnsError_WhenDateFromIsInvalidFormat()
    {
        // Arrange
        var dateFrom = "invalid";

        // Act
        var result = await ArtifactCrudTools.ListArtifacts(null!, dateFrom: dateFrom);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Equal("DateFrom must be in ISO format (YYYY-MM-DD)", result.Error);
    }

    [Fact]
    public async Task ListArtifacts_ReturnsError_WhenDateToIsInvalidFormat()
    {
        // Arrange
        var dateTo = "invalid";

        // Act
        var result = await ArtifactCrudTools.ListArtifacts(null!, dateTo: dateTo);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Equal("DateTo must be in ISO format (YYYY-MM-DD)", result.Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(-999)]
    public async Task ListArtifacts_ReturnsError_WhenOffsetIsNegative_MultipleValues(int offset)
    {
        // Act
        var result = await ArtifactCrudTools.ListArtifacts(null!, offset: offset, limit: 50);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Equal("Offset must be >= 0", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    [InlineData(1000)]
    public async Task ListArtifacts_ReturnsError_WhenLimitIsInvalid_MultipleValues(int limit)
    {
        // Act
        var result = await ArtifactCrudTools.ListArtifacts(null!, offset: 0, limit: limit);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.Equal("Limit must be between 1 and 500", result.Error);
    }

    #endregion

    #region ReadArtifactText Tests

    [Fact]
    public async Task ReadArtifactText_ReturnsError_WhenArtifactIdIsEmpty()
    {
        // Arrange
        var artifactId = "";

        // Act
        var result = await ArtifactCrudTools.ReadArtifactText(artifactId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Artifact ID must not be empty", result.Error);
    }

    [Fact]
    public async Task ReadArtifactText_ReturnsError_WhenArtifactIdIsWhitespace()
    {
        // Arrange
        var artifactId = "   ";

        // Act
        var result = await ArtifactCrudTools.ReadArtifactText(artifactId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Artifact ID must not be empty", result.Error);
    }

    [Fact]
    public async Task ReadArtifactText_ReturnsError_WhenFormatIsInvalid()
    {
        // Arrange
        var artifactId = "test-id";
        var format = "invalid";

        // Act
        var result = await ArtifactCrudTools.ReadArtifactText(artifactId, null!, format: format);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Format must be 'text', 'hex', or 'base64'", result.Error);
    }

    #endregion

    #region DeleteArtifact Tests

    [Fact]
    public async Task DeleteArtifact_ReturnsError_WhenArtifactIdIsEmpty()
    {
        // Arrange
        var artifactId = "";

        // Act
        var result = await ArtifactCrudTools.DeleteArtifact(artifactId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Artifact ID must not be empty", result.Message);
    }

    [Fact]
    public async Task DeleteArtifact_ReturnsError_WhenArtifactIdIsWhitespace()
    {
        // Arrange
        var artifactId = "   ";

        // Act
        var result = await ArtifactCrudTools.DeleteArtifact(artifactId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Artifact ID must not be empty", result.Message);
    }

    #endregion

    #region PinArtifact Tests

    [Fact]
    public async Task PinArtifact_ReturnsError_WhenArtifactIdIsEmpty()
    {
        // Arrange
        var artifactId = "";

        // Act
        var result = await ArtifactLifecycleTools.PinArtifact(artifactId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Artifact ID must not be empty", result.Message);
    }

    [Fact]
    public async Task PinArtifact_ReturnsError_WhenArtifactIdIsWhitespace()
    {
        // Arrange
        var artifactId = "   ";

        // Act
        var result = await ArtifactLifecycleTools.PinArtifact(artifactId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Artifact ID must not be empty", result.Message);
    }

    #endregion

    #region UnpinArtifact Tests

    [Fact]
    public async Task UnpinArtifact_ReturnsError_WhenArtifactIdIsEmpty()
    {
        // Arrange
        var artifactId = "";

        // Act
        var result = await ArtifactLifecycleTools.UnpinArtifact(artifactId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Artifact ID must not be empty", result.Message);
    }

    [Fact]
    public async Task UnpinArtifact_ReturnsError_WhenArtifactIdIsWhitespace()
    {
        // Arrange
        var artifactId = "   ";

        // Act
        var result = await ArtifactLifecycleTools.UnpinArtifact(artifactId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Artifact ID must not be empty", result.Message);
    }

    #endregion

    #region CleanupArtifacts Tests

    [Fact]
    public async Task CleanupArtifacts_ReturnsError_WhenMaxAgeIsEmpty()
    {
        // Arrange
        var maxAge = "";

        // Act
        var result = await ArtifactLifecycleTools.CleanupArtifacts(maxAge, null!);

        // Assert
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal("Max age must not be empty", result.Message);
    }

    [Fact]
    public async Task CleanupArtifacts_ReturnsError_WhenMaxAgeIsWhitespace()
    {
        // Arrange
        var maxAge = "   ";

        // Act
        var result = await ArtifactLifecycleTools.CleanupArtifacts(maxAge, null!);

        // Assert
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal("Max age must not be empty", result.Message);
    }

    [Fact]
    public async Task CleanupArtifacts_ReturnsError_WhenStrategyIsInvalid()
    {
        // Arrange
        var maxAge = "7d";
        var strategy = "invalid";

        // Act
        var result = await ArtifactLifecycleTools.CleanupArtifacts(maxAge, null!, strategy: strategy);

        // Assert
        Assert.Equal(0, result.DeletedCount);
        Assert.Contains("Strategy must be one of", result.Message);
    }

    [Fact]
    public async Task CleanupArtifacts_ReturnsError_WhenMaxSizeBytesIsZero()
    {
        // Arrange
        var maxAge = "7d";
        var maxSizeBytes = 0L;

        // Act
        var result = await ArtifactLifecycleTools.CleanupArtifacts(maxAge, null!, maxSizeBytes: maxSizeBytes);

        // Assert
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal("MaxSizeBytes must be greater than 0", result.Message);
    }

    [Fact]
    public async Task CleanupArtifacts_ReturnsError_WhenMaxSizeBytesIsNegative()
    {
        // Arrange
        var maxAge = "7d";
        var maxSizeBytes = -1L;

        // Act
        var result = await ArtifactLifecycleTools.CleanupArtifacts(maxAge, null!, maxSizeBytes: maxSizeBytes);

        // Assert
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal("MaxSizeBytes must be greater than 0", result.Message);
    }

    #endregion
}
