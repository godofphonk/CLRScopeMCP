using ClrScope.Mcp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Services;

public record HealthResult(
    bool IsHealthy,
    string Version,
    ArtifactRootInfo ArtifactRoot,
    DatabaseInfo Database,
    string[] Warnings
);

public record ArtifactRootInfo(
    bool Exists,
    bool IsWritable,
    string Path,
    long FreeSpaceBytes
);

public record DatabaseInfo(
    bool IsAccessible,
    string Path
);

public class HealthService
{
    private readonly IOptions<ClrScopeOptions> _options;
    private readonly ILogger<HealthService> _logger;

    public HealthService(IOptions<ClrScopeOptions> options, ILogger<HealthService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<HealthResult> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var artifactRoot = _options.Value.GetArtifactRoot();
        var databasePath = _options.Value.GetDatabasePath();

        // Check ArtifactRoot
        var artifactRootExists = Directory.Exists(artifactRoot);
        var artifactRootWritable = false;
        long freeSpaceBytes = 0;

        if (artifactRootExists)
        {
            try
            {
                var testFile = Path.Combine(artifactRoot, ".write_test");
                await File.WriteAllTextAsync(testFile, "test", cancellationToken);
                File.Delete(testFile);
                artifactRootWritable = true;
            }
            catch
            {
                warnings.Add($"Artifact root is not writable: {artifactRoot}");
            }

            try
            {
                var driveInfo = new DriveInfo(artifactRoot);
                freeSpaceBytes = driveInfo.AvailableFreeSpace;
                if (freeSpaceBytes < 100 * 1024 * 1024)
                {
                    warnings.Add($"Low disk space: {freeSpaceBytes / (1024 * 1024)} MB available");
                }
            }
            catch
            {
                // Ignore disk space check errors
            }
        }
        else
        {
            warnings.Add($"Artifact root does not exist: {artifactRoot}");
        }

        // Check Database
        var databaseAccessible = false;
        try
        {
            var dbDirectory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync(cancellationToken);
            databaseAccessible = true;
        }
        catch (Exception ex)
        {
            warnings.Add($"Database is not accessible: {ex.Message}");
        }

        var isHealthy = artifactRootExists && artifactRootWritable && databaseAccessible;

        return new HealthResult(
            isHealthy,
            "0.1.0", // Stage 0a-A
            new ArtifactRootInfo(artifactRootExists, artifactRootWritable, artifactRoot, freeSpaceBytes),
            new DatabaseInfo(databaseAccessible, databasePath),
            warnings.ToArray()
        );
    }
}
