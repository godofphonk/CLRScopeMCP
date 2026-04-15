using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Prepares heap snapshot data from GcDump artifacts using dotnet-gcdump report.
/// Fast/safe implementation for heapstat mode.
/// </summary>
public sealed class HeapSnapshotPreparer : IHeapSnapshotPreparer
{
    private readonly ICliCommandRunner _cliRunner;
    private readonly ILogger<HeapSnapshotPreparer> _logger;

    public HeapSnapshotPreparer(
        ICliCommandRunner cliRunner,
        ILogger<HeapSnapshotPreparer> logger)
    {
        _cliRunner = cliRunner ?? throw new ArgumentNullException(nameof(cliRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PreparedHeapVisualizationData> PrepareAsync(
        Artifact artifact,
        HeapPreparationOptions options,
        CancellationToken cancellationToken,
        IProgress<HeapPreparationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(options);

        if (artifact.Kind != ArtifactKind.GcDump)
            throw new InvalidOperationException($"Artifact '{artifact.ArtifactId.Value}' is not a GcDump.");

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Reading",
            CurrentStep = 1,
            TotalSteps = 3,
            Message = "Running dotnet-gcdump report..."
        });

        // Run dotnet-gcdump report to get heap statistics
        var typeStats = await RunGcDumpReportAsync(artifact.FilePath, cancellationToken);

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Aggregating",
            CurrentStep = 2,
            TotalSteps = 3,
            Message = $"Found {typeStats.Count} types, applying filters..."
        });

        // Apply filters and aggregation
        var filteredStats = FilterAndAggregate(typeStats, options);

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Completed",
            CurrentStep = 3,
            TotalSteps = 3,
            Message = $"Prepared {filteredStats.Count} types for visualization"
        });

        var snapshot = new HeapSnapshotData
        {
            Artifact = artifact,
            Metadata = new HeapMetadata
            {
                TotalHeapBytes = filteredStats.Sum(t => t.ShallowSizeBytes),
                TotalObjectCount = filteredStats.Sum(t => t.Count),
                IsPartial = false,
                Warning = null
            },
            TypeStats = filteredStats
        };

        return new PreparedHeapVisualizationData
        {
            Snapshot = snapshot,
            SuggestedDefaultView = HeapViewKind.TypeDistribution,
            FromCache = false,
            CacheKey = BuildCacheKey(artifact, options)
        };
    }

    private async Task<List<TypeStatData>> RunGcDumpReportAsync(
        string gcdumpPath,
        CancellationToken cancellationToken)
    {
        var args = new[]
        {
            "report",
            gcdumpPath
        };

        _logger.LogInformation("Executing: dotnet-gcdump {Args}", string.Join(" ", args));
        var result = await _cliRunner.ExecuteAsync("dotnet-gcdump", args, cancellationToken);

        if (!result.Success || result.ExitCode != 0)
        {
            var error = !string.IsNullOrEmpty(result.StandardError) ? result.StandardError : result.StandardOutput;
            _logger.LogError("dotnet-gcdump report failed: {Error}", error);
            throw new InvalidOperationException($"dotnet-gcdump report failed: {error}");
        }

        // Parse the output
        return ParseGcDumpReportOutput(result.StandardOutput);
    }

    private List<TypeStatData> ParseGcDumpReportOutput(string output)
    {
        var typeStats = new List<TypeStatData>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // dotnet-gcdump report output format:
        // Type Name, Count, Total Size, Average Size
        // Example:
        // System.String 1000 24000 24
        // System.Collections.Generic.List`1 500 12000 24

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip header lines and non-data lines
            if (trimmed.StartsWith("Type") || 
                trimmed.StartsWith("----") ||
                trimmed.StartsWith("Total") ||
                string.IsNullOrWhiteSpace(trimmed) ||
                trimmed.Length < 10)
            {
                continue;
            }

            // Try to parse the line
            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                var typeName = parts[0];
                
                if (int.TryParse(parts[1], out var count) &&
                    long.TryParse(parts[2], out var totalSize))
                {
                    typeStats.Add(new TypeStatData
                    {
                        TypeName = typeName,
                        Namespace = ExtractNamespace(typeName),
                        AssemblyName = ExtractAssembly(typeName),
                        Generation = "mixed",
                        Count = count,
                        ShallowSizeBytes = totalSize,
                        RetainedSizeBytes = 0 // Not available in heapstat mode
                    });
                }
            }
        }

        return typeStats;
    }

    private List<TypeStatData> FilterAndAggregate(
        List<TypeStatData> typeStats,
        HeapPreparationOptions options)
    {
        var query = typeStats.AsQueryable();

        // Sort by metric
        query = options.Metric switch
        {
            HeapMetricKind.Count => query.OrderByDescending(t => t.Count),
            _ => query.OrderByDescending(t => t.ShallowSizeBytes)
        };

        // Take top N
        var filtered = query.Take(options.MaxTypes).ToList();

        return filtered;
    }

    private static string ExtractNamespace(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var lastDot = typeName.LastIndexOf('.');
        return lastDot > 0 ? typeName[..lastDot] : string.Empty;
    }

    private static string ExtractAssembly(string typeName)
    {
        // In heapstat mode, assembly info is not available
        // This will be populated in future graph-based implementation
        return string.Empty;
    }

    private static string BuildCacheKey(Artifact artifact, HeapPreparationOptions options)
    {
        var fingerprint = artifact.FilePath ?? artifact.ArtifactId.Value;
        return $"heap:{fingerprint}:metric={options.Metric}:group={options.GroupBy}:v=1";
    }
}
