using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Prepares heap snapshot data from GcDump artifacts using dotnet-gcdump report.
/// Fast/safe implementation for heapstat mode.
/// </summary>
public sealed class HeapSnapshotPreparer : IHeapSnapshotPreparer
{
    private readonly ICliCommandRunner _cliRunner;
    private readonly ILogger<HeapSnapshotPreparer> _logger;
    private readonly IHeapSnapshotCache _cache;

    public HeapSnapshotPreparer(
        ICliCommandRunner cliRunner,
        ILogger<HeapSnapshotPreparer> logger,
        IHeapSnapshotCache cache)
    {
        _cliRunner = cliRunner ?? throw new ArgumentNullException(nameof(cliRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
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

        var cacheKey = _cache.GenerateCacheKey(artifact, options);

        // Handle analysis mode
        if (options.AnalysisMode == HeapAnalysisMode.Reuse)
        {
            progress?.Report(new HeapPreparationProgress
            {
                Phase = "Checking cache",
                CurrentStep = 1,
                TotalSteps = 1,
                Message = "Checking cache..."
            });

            if (_cache.TryGet(cacheKey, out var cachedSnapshot) && cachedSnapshot != null)
            {
                progress?.Report(new HeapPreparationProgress
                {
                    Phase = "Completed",
                    CurrentStep = 1,
                    TotalSteps = 1,
                    Message = "Loaded from cache"
                });

                return new PreparedHeapVisualizationData
                {
                    Snapshot = cachedSnapshot,
                    SuggestedDefaultView = HeapViewKind.TypeDistribution,
                    FromCache = true,
                    CacheKey = cacheKey
                };
            }

            progress?.Report(new HeapPreparationProgress
            {
                Phase = "Completed",
                CurrentStep = 1,
                TotalSteps = 1,
                Message = "Cache miss"
            });

            return new PreparedHeapVisualizationData
            {
                Snapshot = new HeapSnapshotData
                {
                    Artifact = artifact,
                    Metadata = new HeapMetadata { IsPartial = false },
                    Nodes = new List<MemoryNodeData>(),
                    Edges = new List<MemoryEdgeData>(),
                    Roots = new List<RootGroupData>(),
                    TypeStats = new List<TypeStatData>(),
                    Dominators = new Dictionary<long, long?>(),
                    RetainedSizes = new Dictionary<long, long>(),
                    Depths = new Dictionary<long, int>()
                },
                SuggestedDefaultView = HeapViewKind.TypeDistribution,
                FromCache = false,
                CacheKey = cacheKey
            };
        }

        if (options.AnalysisMode == HeapAnalysisMode.Force)
        {
            // Force re-analysis
            progress?.Report(new HeapPreparationProgress
            {
                Phase = "Reading",
                CurrentStep = 1,
                TotalSteps = 4,
                Message = "Running dotnet-gcdump report (force)..."
            });

            var typeStats = await RunGcDumpReportAsync(artifact.FilePath, cancellationToken);

            progress?.Report(new HeapPreparationProgress
            {
                Phase = "Aggregating",
                CurrentStep = 2,
                TotalSteps = 4,
                Message = $"Found {typeStats.Count} types, applying filters..."
            });

            var filteredStats = FilterAndAggregate(typeStats, options);

            progress?.Report(new HeapPreparationProgress
            {
                Phase = "Caching",
                CurrentStep = 3,
                TotalSteps = 4,
                Message = "Caching results..."
            });

            var snapshot = BuildSnapshot(artifact, filteredStats);
            _cache.Set(cacheKey, snapshot);

            progress?.Report(new HeapPreparationProgress
            {
                Phase = "Completed",
                CurrentStep = 4,
                TotalSteps = 4,
                Message = $"Prepared {filteredStats.Count} types for visualization"
            });

            return new PreparedHeapVisualizationData
            {
                Snapshot = snapshot,
                SuggestedDefaultView = HeapViewKind.TypeDistribution,
                FromCache = false,
                CacheKey = cacheKey
            };
        }

        // Auto mode
        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Checking cache",
            CurrentStep = 1,
            TotalSteps = 4,
            Message = "Checking cache..."
        });

        if (_cache.TryGet(cacheKey, out var autoCachedSnapshot) && autoCachedSnapshot != null)
        {
            _logger.LogInformation("Using cached heap snapshot for artifact {ArtifactId}", artifact.ArtifactId.Value);
            progress?.Report(new HeapPreparationProgress
            {
                Phase = "Completed",
                CurrentStep = 1,
                TotalSteps = 1,
                Message = "Loaded from cache"
            });

            return new PreparedHeapVisualizationData
            {
                Snapshot = autoCachedSnapshot,
                SuggestedDefaultView = HeapViewKind.TypeDistribution,
                FromCache = true,
                CacheKey = cacheKey
            };
        }

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Reading",
            CurrentStep = 2,
            TotalSteps = 4,
            Message = "Running dotnet-gcdump report..."
        });

        var autoTypeStats = await RunGcDumpReportAsync(artifact.FilePath, cancellationToken);

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Aggregating",
            CurrentStep = 3,
            TotalSteps = 4,
            Message = $"Found {autoTypeStats.Count} types, applying filters..."
        });

        var autoFilteredStats = FilterAndAggregate(autoTypeStats, options);

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Caching",
            CurrentStep = 4,
            TotalSteps = 4,
            Message = "Caching results..."
        });

        var autoSnapshot = BuildSnapshot(artifact, autoFilteredStats);
        _cache.Set(cacheKey, autoSnapshot);

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Completed",
            CurrentStep = 4,
            TotalSteps = 4,
            Message = $"Prepared {autoFilteredStats.Count} types for visualization"
        });

        return new PreparedHeapVisualizationData
        {
            Snapshot = autoSnapshot,
            SuggestedDefaultView = HeapViewKind.TypeDistribution,
            FromCache = false,
            CacheKey = cacheKey
        };
    }

    private async Task<List<TypeStatData>> RunGcDumpReportAsync(
        string gcdumpPath,
        CancellationToken cancellationToken)
    {
        // Use ClrScope.HeapParser instead of dotnet-gcdump for proper timeout handling
        var heapParserPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClrScope.HeapParser.dll");
        
        if (!File.Exists(heapParserPath))
        {
            throw new FileNotFoundException($"Heap parser not found at: {heapParserPath}");
        }

        var args = new[]
        {
            heapParserPath,
            "gcdump",
            gcdumpPath
        };

        _logger.LogInformation("Executing: dotnet {Args}", string.Join(" ", args));
        var result = await _cliRunner.ExecuteAsync("dotnet", args, cancellationToken);

        if (!result.Success || result.ExitCode != 0)
        {
            var error = !string.IsNullOrEmpty(result.StandardError) ? result.StandardError : result.StandardOutput;
            _logger.LogError("HeapParser failed: {Error}", error);
            throw new InvalidOperationException($"HeapParser failed: {error}");
        }

        // Parse JSON output from HeapParser
        return ParseHeapParserJsonOutput(result.StandardOutput);
    }

    private List<TypeStatData> ParseHeapParserJsonOutput(string output)
    {
        try
        {
            var graphData = JsonSerializer.Deserialize<HeapGraphData>(output, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (graphData == null || graphData.Nodes.Count == 0)
            {
                _logger.LogWarning("HeapParser returned empty graph data");
                return new List<TypeStatData>();
            }

            // Aggregate nodes by type name to create TypeStatData
            var typeStats = new Dictionary<string, TypeStatData>();

            foreach (var node in graphData.Nodes.Values)
            {
                if (!typeStats.TryGetValue(node.TypeName, out var stat))
                {
                    stat = new TypeStatData
                    {
                        TypeName = node.TypeName,
                        Namespace = ExtractNamespace(node.TypeName),
                        AssemblyName = node.AssemblyName,
                        Generation = node.Generation,
                        Count = node.Count,
                        ShallowSizeBytes = node.ShallowSizeBytes,
                        RetainedSizeBytes = node.RetainedSizeBytes
                    };
                    typeStats[node.TypeName] = stat;
                }
                else
                {
                    // Create new object with aggregated values
                    typeStats[node.TypeName] = new TypeStatData
                    {
                        TypeName = stat.TypeName,
                        Namespace = stat.Namespace,
                        AssemblyName = stat.AssemblyName,
                        Generation = stat.Generation,
                        Count = stat.Count + node.Count,
                        ShallowSizeBytes = stat.ShallowSizeBytes + node.ShallowSizeBytes,
                        RetainedSizeBytes = stat.RetainedSizeBytes + node.RetainedSizeBytes
                    };
                }
            }

            return typeStats.Values.ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse HeapParser JSON output");
            throw new InvalidOperationException("Failed to parse HeapParser JSON output", ex);
        }
    }

    private List<TypeStatData> FilterAndAggregate(
        List<TypeStatData> typeStats,
        HeapPreparationOptions options)
    {
        var query = typeStats.AsQueryable();

        // Group by
        if (options.GroupBy == HeapGroupBy.Namespace)
        {
            query = query.GroupBy(t => t.Namespace)
                .Select(g => new TypeStatData
                {
                    TypeName = g.Key ?? "<global>",
                    Namespace = g.Key ?? string.Empty,
                    AssemblyName = string.Empty,
                    Generation = "mixed",
                    Count = g.Sum(t => t.Count),
                    ShallowSizeBytes = g.Sum(t => t.ShallowSizeBytes),
                    RetainedSizeBytes = 0
                });
        }
        else if (options.GroupBy == HeapGroupBy.Assembly)
        {
            query = query.GroupBy(t => t.AssemblyName)
                .Select(g => new TypeStatData
                {
                    TypeName = g.Key ?? "<unknown>",
                    Namespace = string.Empty,
                    AssemblyName = g.Key ?? string.Empty,
                    Generation = "mixed",
                    Count = g.Sum(t => t.Count),
                    ShallowSizeBytes = g.Sum(t => t.ShallowSizeBytes),
                    RetainedSizeBytes = 0
                });
        }

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

    private static HeapSnapshotData BuildSnapshot(Artifact artifact, List<TypeStatData> filteredStats)
    {
        return new HeapSnapshotData
        {
            Artifact = artifact,
            Metadata = new HeapMetadata
            {
                RuntimeVersion = string.Empty,
                ToolVersion = string.Empty,
                TotalHeapBytes = filteredStats.Sum(t => t.ShallowSizeBytes),
                TotalObjectCount = filteredStats.Sum(t => t.Count),
                RootCount = 0,
                SegmentCount = 0,
                IsPartial = false,
                Warning = null
            },
            Nodes = new List<MemoryNodeData>(),
            Edges = new List<MemoryEdgeData>(),
            Roots = new List<RootGroupData>(),
            TypeStats = filteredStats,
            Dominators = new Dictionary<long, long?>(),
            RetainedSizes = new Dictionary<long, long>(),
            Depths = new Dictionary<long, int>()
        };
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
}
