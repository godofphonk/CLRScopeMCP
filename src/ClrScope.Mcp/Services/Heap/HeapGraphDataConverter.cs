using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Heap;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Converts HeapGraphData to HeapSnapshotData for rendering.
/// </summary>
public sealed class HeapGraphDataConverter
{
    private readonly ILogger<HeapGraphDataConverter> _logger;

    public HeapGraphDataConverter(ILogger<HeapGraphDataConverter> logger)
    {
        _logger = logger;
    }

    public HeapSnapshotData ConvertToSnapshot(
        HeapGraphData graphData,
        Artifact artifact)
    {
        _logger.LogInformation("Converting HeapGraphData to HeapSnapshotData");

        // Aggregate type statistics from nodes
        var typeStats = AggregateTypeStats(graphData.Nodes);

        // Calculate totals
        var totalHeapBytes = typeStats.Sum(t => t.ShallowSizeBytes);
        var totalObjectCount = typeStats.Sum(t => t.Count);

        var metadata = new HeapMetadata
        {
            RuntimeVersion = "unknown",
            ToolVersion = "heap-parser-process",
            TotalHeapBytes = totalHeapBytes,
            TotalObjectCount = totalObjectCount,
            RootCount = graphData.Roots.Count,
            SegmentCount = 1,
            IsPartial = false
        };

        return new HeapSnapshotData
        {
            Artifact = artifact,
            Metadata = metadata,
            Nodes = graphData.Nodes.Values.ToList().AsReadOnly(),
            Edges = graphData.Edges.AsReadOnly(),
            Roots = graphData.Roots.AsReadOnly(),
            TypeStats = typeStats,
            Dominators = new Dictionary<long, long?>(),
            RetainedSizes = new Dictionary<long, long>(),
            Depths = new Dictionary<long, int>()
        };
    }

    private static List<TypeStatData> AggregateTypeStats(Dictionary<long, MemoryNodeData> nodes)
    {
        var typeGroups = nodes.Values
            .GroupBy(n => n.TypeName)
            .Select(g => new TypeStatData
            {
                TypeName = g.Key,
                Namespace = ExtractNamespace(g.Key),
                Count = g.Sum(n => n.Count),
                ShallowSizeBytes = g.Sum(n => n.ShallowSizeBytes)
            })
            .OrderByDescending(t => t.ShallowSizeBytes)
            .ToList();

        return typeGroups;
    }

    private static string ExtractNamespace(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "";

        var lastDot = typeName.LastIndexOf('.');
        return lastDot > 0 ? typeName.Substring(0, lastDot) : "";
    }
}
