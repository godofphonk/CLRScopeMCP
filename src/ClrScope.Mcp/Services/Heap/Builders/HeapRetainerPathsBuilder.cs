using ClrScope.Mcp.Domain.Heap.Data;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Builder for retainer paths from roots to target object.
/// </summary>
public sealed class HeapRetainerPathsBuilder
{
    private readonly ILogger<HeapRetainerPathsBuilder> _logger;
    private readonly DominatorTreeCalculator _dominatorCalculator;

    public HeapRetainerPathsBuilder(
        ILogger<HeapRetainerPathsBuilder> logger,
        DominatorTreeCalculator dominatorCalculator)
    {
        _logger = logger;
        _dominatorCalculator = dominatorCalculator;
    }

    public RetainerPathData BuildRetainerPaths(HeapGraphData graph, string targetObjectId, int maxPaths = 10)
    {
        _logger.LogInformation("Building retainer paths for target object {TargetObjectId} (max {MaxPaths} paths)",
            targetObjectId, maxPaths);

        // Parse target object ID
        if (!long.TryParse(targetObjectId, out var targetNodeId))
        {
            _logger.LogWarning("Invalid target object ID: {TargetObjectId}", targetObjectId);
            return new RetainerPathData
            {
                TargetObjectId = targetObjectId,
                TargetTypeName = "Unknown",
                RetainerChains = new List<RetainerChainData>()
            };
        }

        // Get target node
        var targetNode = graph.Nodes.GetValueOrDefault(targetNodeId);
        if (targetNode == null)
        {
            _logger.LogWarning("Target node not found: {TargetNodeId}", targetNodeId);
            return new RetainerPathData
            {
                TargetObjectId = targetObjectId,
                TargetTypeName = "Unknown",
                RetainerChains = new List<RetainerChainData>()
            };
        }

        // Use DominatorTreeCalculator to find retainer paths
        var retainerPaths = _dominatorCalculator.FindRetainerPaths(graph, targetNodeId, maxPaths);

        // Convert RetainerPath to RetainerChainData
        var chains = retainerPaths.Select(rp => new RetainerChainData
        {
            RetainedSizeBytes = targetNode.RetainedSizeBytes,
            Steps = rp.Steps.Select(step =>
            {
                var fromNode = graph.Nodes.GetValueOrDefault(step.FromNodeId);
                return new RetainerStepData
                {
                    ObjectId = step.FromNodeId.ToString(),
                    TypeName = fromNode?.TypeName ?? "Unknown",
                    Namespace = fromNode?.Namespace ?? string.Empty,
                    AssemblyName = fromNode?.AssemblyName ?? string.Empty,
                    FieldName = step.EdgeKind,
                    ShallowSizeBytes = fromNode?.ShallowSizeBytes ?? 0,
                    IsRoot = graph.Nodes.GetValueOrDefault(step.FromNodeId)?.IsRoot ?? false
                };
            }).ToList()
        }).ToList();

        return new RetainerPathData
        {
            TargetObjectId = targetObjectId,
            TargetTypeName = targetNode.TypeName,
            RetainerChains = chains
        };
    }
}
