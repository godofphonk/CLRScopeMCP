using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Domain.Heap.Envelopes;
using ClrScope.Mcp.Domain.Heap.Facades;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Mapper for converting MemoryGraph to HeapSnapshotData.
/// </summary>
public interface IHeapSnapshotMapper
{
    HeapSnapshotData Map(
        Artifact artifact,
        MemoryGraphEnvelope envelope,
        IMemoryGraphFacade facade);
}

/// <summary>
/// Implementation of MemoryGraph to HeapSnapshotData mapper.
/// </summary>
public sealed class MemoryGraphHeapSnapshotMapper : IHeapSnapshotMapper
{
    private readonly ILogger<MemoryGraphHeapSnapshotMapper> _logger;

    public MemoryGraphHeapSnapshotMapper(ILogger<MemoryGraphHeapSnapshotMapper> logger)
    {
        _logger = logger;
    }

    public HeapSnapshotData Map(
        Artifact artifact,
        MemoryGraphEnvelope envelope,
        IMemoryGraphFacade facade)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(facade);

        if (envelope.MemoryGraph == null)
        {
            _logger.LogError("MemoryGraphHeapSnapshotMapper.Map: envelope.MemoryGraph is null");
            throw new ArgumentNullException(nameof(envelope.MemoryGraph), "MemoryGraph is null in envelope");
        }

        var nodes = facade.GetNodes(envelope.MemoryGraph) ?? new List<GraphNodeRecord>();
        var edges = facade.GetEdges(envelope.MemoryGraph) ?? new List<GraphEdgeRecord>();
        var roots = facade.GetRoots(envelope.MemoryGraph) ?? new List<GraphRootRecord>();

        _logger.LogInformation("MemoryGraphHeapSnapshotMapper.Map: {NodeCount} nodes, {EdgeCount} edges, {RootCount} roots",
            nodes.Count, edges.Count, roots.Count);

        var rootNodeIds = roots.Select(r => r.RootNodeId).ToHashSet();
        var rootKindByNodeId = roots
            .GroupBy(r => r.RootNodeId)
            .ToDictionary(g => g.Key, g => g.First().RootKind);

        var memoryNodes = nodes.Select(n => new MemoryNodeData
        {
            NodeId = n.NodeId,
            Address = n.Address,
            TypeName = n.TypeName,
            Namespace = ExtractNamespace(n.TypeName),
            AssemblyName = n.AssemblyName,
            ShallowSizeBytes = n.ShallowSizeBytes,
            RetainedSizeBytes = 0,
            Count = Math.Max(1, n.Count),
            Generation = ResolveGeneration(n.Address, envelope.HeapInfo),
            IsRoot = rootNodeIds.Contains(n.NodeId),
            RootKind = rootKindByNodeId.GetValueOrDefault(n.NodeId),
            DominatorNodeId = null
        }).ToArray();

        var memoryEdges = edges.Select(e => new MemoryEdgeData
        {
            FromNodeId = e.FromNodeId,
            ToNodeId = e.ToNodeId,
            EdgeKind = e.EdgeKind,
            IsWeak = e.IsWeak
        }).ToArray();

        var typeStats = memoryNodes
            .GroupBy(n => new { n.TypeName, n.AssemblyName, n.Generation })
            .Select(g => new TypeStatData
            {
                TypeName = g.Key.TypeName,
                Namespace = g.First().Namespace,
                AssemblyName = g.Key.AssemblyName,
                Generation = g.Key.Generation,
                Count = g.Sum(x => x.Count),
                ShallowSizeBytes = g.Sum(x => x.ShallowSizeBytes),
                RetainedSizeBytes = 0
            })
            .OrderByDescending(x => x.ShallowSizeBytes)
            .ToArray();

        var rootStats = memoryNodes
            .Where(n => n.IsRoot)
            .GroupBy(n => n.RootKind ?? "other")
            .Select(g => new RootGroupData
            {
                RootKind = g.Key,
                RootCount = g.Count(),
                ReachableBytes = g.Sum(x => x.ShallowSizeBytes),
                RetainedBytes = 0
            })
            .OrderByDescending(x => x.RootCount)
            .ToArray();

        var heapInfo = envelope.HeapInfo;
        var segments = heapInfo?.Segments ?? new List<HeapSegmentInfo>();

        return new HeapSnapshotData
        {
            Artifact = artifact,
            Metadata = new HeapMetadata
            {
                RuntimeVersion = envelope.Metadata.RuntimeVersion,
                ToolVersion = envelope.Metadata.ToolVersion,
                TotalHeapBytes = memoryNodes.Sum(n => n.ShallowSizeBytes),
                TotalObjectCount = memoryNodes.Sum(n => (long)n.Count),
                RootCount = rootStats.Sum(r => r.RootCount),
                SegmentCount = segments.Count,
                IsPartial = envelope.Metadata.IsPartial,
                Warning = envelope.Metadata.Warning
            },
            Nodes = memoryNodes,
            Edges = memoryEdges,
            Roots = rootStats,
            TypeStats = typeStats,
            Dominators = new Dictionary<long, long?>(),
            RetainedSizes = new Dictionary<long, long>(),
            Depths = new Dictionary<long, int>()
        };
    }

    private static string ExtractNamespace(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var idx = typeName.LastIndexOf('.');
        return idx > 0 ? typeName[..idx] : string.Empty;
    }

    private static string ResolveGeneration(ulong? address, DotNetHeapInfoAdapter heapInfo)
    {
        if (address is null)
            return "unknown";

        if (heapInfo?.Segments == null)
            return "unknown";

        foreach (var segment in heapInfo.Segments)
        {
            if (address.Value < segment.Start || address.Value >= segment.End)
                continue;

            if (segment.Gen0End > 0 && address.Value < segment.Gen0End) return "gen0";
            if (segment.Gen1End > 0 && address.Value < segment.Gen1End) return "gen1";
            if (segment.Gen2End > 0 && address.Value < segment.Gen2End) return "gen2";
            if (segment.Gen3End > 0 && address.Value < segment.Gen3End) return "loh";
            if (segment.Gen4End > 0 && address.Value < segment.Gen4End) return "poh";

            return "unknown";
        }

        return "unknown";
    }
}
