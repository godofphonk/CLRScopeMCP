using ClrScope.Mcp.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClrScope.Mcp.Options;

namespace ClrScope.Mcp.Services.Artifacts;

public interface IArtifactPathValidatorService
{
    void ValidateArtifactPath(string filePath, string artifactRoot);
}

public class ArtifactPathValidatorService : IArtifactPathValidatorService
{
    private readonly ILogger<ArtifactPathValidatorService> _logger;

    public ArtifactPathValidatorService(ILogger<ArtifactPathValidatorService> logger)
    {
        _logger = logger;
    }

    public void ValidateArtifactPath(string filePath, string artifactRoot)
    {
        try
        {
            PathSecurity.EnsurePathWithinDirectory(filePath, artifactRoot);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Path validation failed: {FilePath} is outside artifact root {ArtifactRoot}", filePath, artifactRoot);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Path validation failed for {FilePath}", filePath);
            throw new UnauthorizedAccessException("Invalid file path", ex);
        }
    }
}
