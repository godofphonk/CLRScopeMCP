using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Domain.Heap.Enums;
using ClrScope.Mcp.Domain.Heap.Options;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClrScope.Mcp.Tools.Analysis;

[McpServerToolType]
public sealed class HeapAnalysisTools
{
    // NOTE: EventPipe heap analysis (.nettrace) removed due to vendored library issues
    // The EventPipeDotNetHeapDumper library has problems constructing MemoryGraph from EventPipe events
    // Use .gcdump files instead: mcp1_collect_gcdump + mcp1_analyze_heap

    [McpServerTool(Name = "analyze_heap"), Description("Analyze a .gcdump heap snapshot: type statistics (top N types), object list (with node IDs for find_retainer_paths), or diff comparison between two gcdumps. Returns JSON/text output only.")]
    public static async Task<HeapAnalysisResult> AnalyzeHeap(
        string artifactId,
        McpServer server,
        [Description("Analysis type: 'type_stats' (default) for top N types, 'objects' for object list with node IDs, 'diff' for comparison between two snapshots")] string analysisType = "type_stats",
        [Description("Metric: 'shallow_size' (default), 'count'")] string metric = "shallow_size",
        [Description("Analysis mode: 'auto' (default), 'reuse' (cache only), 'force' (re-analyze)")] string analysisMode = "auto",
        [Description("Maximum types to include in type_stats output, or maximum objects to include in objects output")] int maxTypes = 50,
        [Description("Baseline artifact ID for diff comparison")] string? baselineArtifactId = null,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<HeapAnalysisTools>>();
        var preparer = server.Services!.GetRequiredService<IHeapSnapshotPreparer>();

        // Add 5-minute timeout to prevent hanging
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), linkedCts.Token);
            if (artifact == null)
            {
                return HeapAnalysisResult.Failure($"Artifact not found: {artifactId}");
            }

            if (artifact.Kind != ArtifactKind.GcDump)
            {
                return HeapAnalysisResult.Failure($"Artifact {artifactId} is not a GcDump.");
            }

            var metricKind = metric.ToLowerInvariant() switch
            {
                "count" => HeapMetricKind.Count,
                _ => HeapMetricKind.ShallowSize
            };

            var analysisModeKind = analysisMode.ToLowerInvariant() switch
            {
                "reuse" => HeapAnalysisMode.Reuse,
                "force" => HeapAnalysisMode.Force,
                _ => HeapAnalysisMode.Auto
            };

            var options = new HeapPreparationOptions
            {
                Metric = metricKind,
                AnalysisMode = analysisModeKind,
                GroupBy = HeapGroupBy.Type,
                MaxTypes = maxTypes
            };

            if (analysisType.ToLowerInvariant() == "diff")
            {
                if (string.IsNullOrEmpty(baselineArtifactId))
                {
                    return HeapAnalysisResult.Failure("baselineArtifactId is required for diff analysis");
                }

                var baselineArtifact = await artifactStore.GetAsync(new ArtifactId(baselineArtifactId), linkedCts.Token);
                if (baselineArtifact == null)
                {
                    return HeapAnalysisResult.Failure($"Baseline artifact not found: {baselineArtifactId}");
                }

                if (baselineArtifact.Kind != ArtifactKind.GcDump)
                {
                    return HeapAnalysisResult.Failure($"Baseline artifact {baselineArtifactId} is not a GcDump.");
                }

                var baselinePrepared = await preparer.PrepareAsync(baselineArtifact, options, linkedCts.Token);
                var targetPrepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

                var differLogger = server.Services!.GetRequiredService<ILogger<HeapSnapshotDiffer>>();
                var differ = new HeapSnapshotDiffer(differLogger);
                var diff = differ.Diff(baselinePrepared.Snapshot, targetPrepared.Snapshot);

                var diffData = new HeapDiffAnalysisData
                {
                    BaselineArtifactId = baselineArtifactId,
                    TargetArtifactId = artifactId,
                    Metric = metric.ToLowerInvariant(),
                    TotalHeapBytesBaseline = baselinePrepared.Snapshot.Metadata.TotalHeapBytes,
                    TotalHeapBytesTarget = targetPrepared.Snapshot.Metadata.TotalHeapBytes,
                    TotalObjectCountBaseline = baselinePrepared.Snapshot.Metadata.TotalObjectCount,
                    TotalObjectCountTarget = targetPrepared.Snapshot.Metadata.TotalObjectCount,
                    TypeDiffs = diff.TypeDiffs.Take(maxTypes).Select(td => new TypeDiffData
                    {
                        TypeName = td.TypeName,
                        Namespace = td.Namespace,
                        Status = td.Status.ToString(),
                        BaselineCount = td.BaselineCount,
                        TargetCount = td.TargetCount,
                        CountDelta = td.CountDelta,
                        BaselineShallowSize = td.BaselineShallowSize,
                        TargetShallowSize = td.TargetShallowSize,
                        ShallowSizeDelta = td.ShallowSizeDelta,
                        ShallowSizePercentChange = td.ShallowSizePercentChange
                    }).ToList()
                };

                return HeapAnalysisResult.Success(JsonSerializer.Serialize(diffData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }
            else if (analysisType.ToLowerInvariant() == "objects")
            {
                logger.LogInformation("Preparing heap snapshot for artifact {ArtifactId} (objects mode)", artifactId);
                var prepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

                // Sort nodes by metric (shallow size or count) and take top N
                var sortedNodes = metric.ToLowerInvariant() == "count"
                    ? prepared.Snapshot.Nodes.OrderByDescending(n => n.Count)
                    : prepared.Snapshot.Nodes.OrderByDescending(n => n.ShallowSizeBytes);

                var objectsData = new HeapObjectListData
                {
                    ArtifactId = artifactId,
                    Metric = metric.ToLowerInvariant(),
                    TotalHeapBytes = prepared.Snapshot.Metadata.TotalHeapBytes,
                    TotalObjectCount = prepared.Snapshot.Metadata.TotalObjectCount,
                    Objects = sortedNodes.Take(maxTypes).Select(node => new HeapObjectData
                    {
                        NodeId = node.NodeId.ToString(),
                        TypeName = node.TypeName,
                        Namespace = node.Namespace,
                        AssemblyName = node.AssemblyName,
                        ShallowSizeBytes = node.ShallowSizeBytes,
                        RetainedSizeBytes = node.RetainedSizeBytes,
                        Count = node.Count,
                        Generation = node.Generation
                    }).ToList()
                };

                return HeapAnalysisResult.Success(JsonSerializer.Serialize(objectsData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }
            else
            {
                logger.LogInformation("Preparing heap snapshot for artifact {ArtifactId}", artifactId);
                var prepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

                var typeStatsData = new HeapTypeStatsData
                {
                    ArtifactId = artifactId,
                    Metric = metric.ToLowerInvariant(),
                    TotalHeapBytes = prepared.Snapshot.Metadata.TotalHeapBytes,
                    TotalObjectCount = prepared.Snapshot.Metadata.TotalObjectCount,
                    TypeStats = prepared.Snapshot.TypeStats.Take(maxTypes).Select(ts => new TypeStatData
                    {
                        TypeName = ts.TypeName,
                        Namespace = ts.Namespace,
                        Count = ts.Count,
                        ShallowSizeBytes = ts.ShallowSizeBytes,
                        RetainedSizeBytes = ts.RetainedSizeBytes
                    }).ToList()
                };

                return HeapAnalysisResult.Success(JsonSerializer.Serialize(typeStatsData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogError("AnalyzeHeap timed out after 5 minutes for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure("AnalyzeHeap timed out after 5 minutes - the operation may be hanging");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("AnalyzeHeap was cancelled for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure("AnalyzeHeap was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Heap analysis failed for artifact {ArtifactId}. Stack: {StackTrace}", artifactId, ex.StackTrace);
            return HeapAnalysisResult.Failure($"Heap analysis failed: {ex.Message}\nStack: {ex.StackTrace}");
        }
    }

    [McpServerTool(Name = "find_retainer_paths"), Description("Find retainer paths from roots to a target object in heap snapshot. Returns JSON with paths showing object retention chains.")]
    public static async Task<HeapAnalysisResult> FindRetainerPaths(
        string artifactId,
        string targetNodeId,
        McpServer server,
        [Description("Maximum number of paths to return (default: 10)")] int maxPaths = 10,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<HeapAnalysisTools>>();
        var preparer = server.Services!.GetRequiredService<IHeapSnapshotPreparer>();
        var dominatorCalculator = server.Services!.GetRequiredService<DominatorTreeCalculator>();

        // Add 5-minute timeout to prevent hanging
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), linkedCts.Token);
            if (artifact == null)
            {
                return HeapAnalysisResult.Failure($"Artifact not found: {artifactId}");
            }

            if (artifact.Kind != ArtifactKind.GcDump)
            {
                return HeapAnalysisResult.Failure($"Artifact {artifactId} is not a GcDump.");
            }

            // Parse target node ID
            if (!long.TryParse(targetNodeId, out var targetNodeIdLong))
            {
                return HeapAnalysisResult.Failure($"Invalid target node ID: {targetNodeId}");
            }

            // Prepare heap snapshot
            var options = new HeapPreparationOptions
            {
                Metric = HeapMetricKind.ShallowSize,
                AnalysisMode = HeapAnalysisMode.Auto,
                GroupBy = HeapGroupBy.Type,
                MaxTypes = 100
            };

            logger.LogInformation("Preparing heap snapshot for artifact {ArtifactId}", artifactId);
            var prepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

            // Build graph from snapshot
            var graph = new HeapGraphData
            {
                Nodes = prepared.Snapshot.Nodes.ToDictionary(n => n.NodeId),
                Edges = prepared.Snapshot.Edges.ToList(),
                Roots = prepared.Snapshot.Roots.ToList()
            };

            // Find retainer paths using DominatorTreeCalculator
            logger.LogInformation("Finding retainer paths for target node {TargetNodeId}", targetNodeId);
            var retainerPaths = dominatorCalculator.FindRetainerPaths(graph, targetNodeIdLong, maxPaths);

            // Convert to JSON
            var pathsData = retainerPaths.Select(rp => new RetainerPathData
            {
                RootNodeId = rp.RootNodeId,
                RootKind = rp.RootKind,
                TotalSteps = rp.TotalSteps,
                Steps = rp.Steps.Select(s => new RetainerPathStepData
                {
                    FromNodeId = s.FromNodeId,
                    ToNodeId = s.ToNodeId,
                    EdgeKind = s.EdgeKind,
                    IsWeak = s.IsWeak
                }).ToList()
            }).ToList();

            var result = new RetainerPathsResult
            {
                TargetNodeId = targetNodeId,
                PathCount = pathsData.Count,
                MaxPaths = maxPaths,
                Paths = pathsData
            };

            return HeapAnalysisResult.Success(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogError("FindRetainerPaths timed out after 5 minutes for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure("FindRetainerPaths timed out after 5 minutes - the operation may be hanging");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("FindRetainerPaths was cancelled for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure("FindRetainerPaths was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Find retainer paths failed for artifact {ArtifactId}", artifactId);
            return HeapAnalysisResult.Failure($"Find retainer paths failed: {ex.Message}");
        }
    }
}

public record HeapAnalysisResult(
    bool IsSuccess,
    string Content,
    string? Error)
{
    public static HeapAnalysisResult Success(string content) =>
        new(true, content, null);

    public static HeapAnalysisResult Failure(string error) =>
        new(false, string.Empty, error);
}

public record HeapTypeStatsData
{
    public string ArtifactId { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public long TotalHeapBytes { get; init; }
    public long TotalObjectCount { get; init; }
    public List<TypeStatData> TypeStats { get; init; } = new();
}

public record HeapObjectListData
{
    public string ArtifactId { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public long TotalHeapBytes { get; init; }
    public long TotalObjectCount { get; init; }
    public List<HeapObjectData> Objects { get; init; } = new();
}

public record HeapObjectData
{
    public string NodeId { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public long ShallowSizeBytes { get; init; }
    public long RetainedSizeBytes { get; init; }
    public int Count { get; init; }
    public string Generation { get; init; } = string.Empty;
}

public record HeapDiffAnalysisData
{
    public string BaselineArtifactId { get; init; } = string.Empty;
    public string TargetArtifactId { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public long TotalHeapBytesBaseline { get; init; }
    public long TotalHeapBytesTarget { get; init; }
    public long TotalObjectCountBaseline { get; init; }
    public long TotalObjectCountTarget { get; init; }
    public List<TypeDiffData> TypeDiffs { get; init; } = new();
}

public record TypeDiffData
{
    public string TypeName { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long BaselineCount { get; init; }
    public long TargetCount { get; init; }
    public long CountDelta { get; init; }
    public long BaselineShallowSize { get; init; }
    public long TargetShallowSize { get; init; }
    public long ShallowSizeDelta { get; init; }
    public double ShallowSizePercentChange { get; init; }
}

public record StackFrameData
{
    public int ThreadId { get; init; }
    public int FrameIndex { get; init; }
    public string CallSite { get; init; } = string.Empty;
}

public record StackFrame(string ChildSP, string IP, string CallSite);

public record ThreadStack(int ThreadId, string? ThreadName, StackFrame[] Frames);

public record StacksOutput(int Pid, DateTime Timestamp, ThreadStack[] Threads);

// Retainer paths result for MCP tool
public record RetainerPathsResult
{
    public string TargetNodeId { get; init; } = string.Empty;
    public int PathCount { get; init; }
    public int MaxPaths { get; init; }
    public required List<RetainerPathData> Paths { get; init; }
}

// Retainer path data for MCP tool
public record RetainerPathData
{
    public long RootNodeId { get; init; }
    public string RootKind { get; init; } = string.Empty;
    public int TotalSteps { get; init; }
    public required List<RetainerPathStepData> Steps { get; init; }
}

// Retainer path step data for MCP tool
public record RetainerPathStepData
{
    public long FromNodeId { get; init; }
    public long ToNodeId { get; init; }
    public string EdgeKind { get; init; } = string.Empty;
    public bool IsWeak { get; init; }
}
