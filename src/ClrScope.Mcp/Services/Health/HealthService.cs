using System.Reflection;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Services.Health;

public record HealthResult(
    bool IsHealthy,
    string Version,
    ArtifactRootInfo ArtifactRoot,
    DatabaseInfo Database,
    CapabilitiesInfo Capabilities,
    ReadinessInfo Readiness,
    string[] Warnings
);

public record CapabilitiesInfo(
    bool DotnetDumpAvailable,
    bool DotnetSymbolAvailable,
    bool DotnetGcdumpAvailable,
    bool DotnetStackAvailable,
    bool DotnetCountersAvailable,
    string[] MissingTools
);

public record ReadinessInfo(
    bool CanCollectDump,
    bool CanCollectTrace,
    bool CanCollectStacks,
    bool CanCollectCounters,
    bool CanResolveSymbols,
    string[] Limitations
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
    private readonly ICliToolAvailabilityChecker _toolChecker;

    public HealthService(IOptions<ClrScopeOptions> options, ILogger<HealthService> logger, ICliToolAvailabilityChecker toolChecker)
    {
        _options = options;
        _logger = logger;
        _toolChecker = toolChecker;
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
                var testFileName = Path.GetRandomFileName();
                var testFile = Path.Combine(artifactRoot, testFileName);
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

        // Check CLI tool availability (capabilities)
        var missingTools = new List<string>();
        var dotnetDumpAvailable = _toolChecker.CheckAvailabilitySync("dotnet-dump").IsAvailable;
        var dotnetSymbolAvailable = _toolChecker.CheckAvailabilitySync("dotnet-symbol").IsAvailable;
        var dotnetGcdumpAvailable = _toolChecker.CheckAvailabilitySync("dotnet-gcdump").IsAvailable;
        var dotnetStackAvailable = _toolChecker.CheckAvailabilitySync("dotnet-stack").IsAvailable;
        var dotnetCountersAvailable = _toolChecker.CheckAvailabilitySync("dotnet-counters").IsAvailable;

        if (!dotnetDumpAvailable) missingTools.Add("dotnet-dump");
        if (!dotnetSymbolAvailable) missingTools.Add("dotnet-symbol");
        if (!dotnetGcdumpAvailable) missingTools.Add("dotnet-gcdump");
        if (!dotnetStackAvailable) missingTools.Add("dotnet-stack");
        if (!dotnetCountersAvailable) missingTools.Add("dotnet-counters");

        var capabilities = new CapabilitiesInfo(
            DotnetDumpAvailable: dotnetDumpAvailable,
            DotnetSymbolAvailable: dotnetSymbolAvailable,
            DotnetGcdumpAvailable: dotnetGcdumpAvailable,
            DotnetStackAvailable: dotnetStackAvailable,
            DotnetCountersAvailable: dotnetCountersAvailable,
            MissingTools: missingTools.ToArray()
        );

        // Determine readiness based on capabilities
        var limitations = new List<string>();
        var canCollectDump = true; // Native dump always available via DiagnosticsClient
        var canCollectTrace = true; // Native trace always available via DiagnosticsClient
        var canCollectStacks = dotnetStackAvailable;
        var canCollectCounters = dotnetCountersAvailable;
        var canResolveSymbols = dotnetSymbolAvailable;

        if (!canCollectStacks) limitations.Add("Stack collection requires dotnet-stack CLI tool");
        if (!canCollectCounters) limitations.Add("Counter collection requires dotnet-counters CLI tool");
        if (!canResolveSymbols) limitations.Add("Symbol resolution requires dotnet-symbol CLI tool");

        var readiness = new ReadinessInfo(
            CanCollectDump: canCollectDump,
            CanCollectTrace: canCollectTrace,
            CanCollectStacks: canCollectStacks,
            CanCollectCounters: canCollectCounters,
            CanResolveSymbols: canResolveSymbols,
            Limitations: limitations.ToArray()
        );

        var isHealthy = artifactRootExists && artifactRootWritable && databaseAccessible;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        return new HealthResult(
            isHealthy,
            version,
            new ArtifactRootInfo(artifactRootExists, artifactRootWritable, artifactRoot, freeSpaceBytes),
            new DatabaseInfo(databaseAccessible, databasePath),
            capabilities,
            readiness,
            warnings.ToArray()
        );
    }
}
