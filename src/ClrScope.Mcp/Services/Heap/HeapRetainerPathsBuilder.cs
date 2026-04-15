using ClrScope.Mcp.Domain.Heap;
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

    public RetainerPathData BuildRetainerPaths(HeapGraphData graph, string targetObjectId)
    {
        _logger.LogInformation("Building retainer paths for target object {TargetObjectId}", targetObjectId);

        // Simplified retainer path building
        // Full implementation would traverse dominator tree from roots to target

        var chains = new List<RetainerChainData>();

        // For now, return empty chains as placeholder
        // Full implementation would:
        // 1. Find dominator tree for target object
        // 2. Trace paths from all roots to target
        // 3. Build retainer chains with step-by-step information

        return new RetainerPathData
        {
            TargetObjectId = targetObjectId,
            TargetTypeName = graph.Nodes.GetValueOrDefault(targetObjectId)?.TypeName ?? "Unknown",
            RetainerChains = chains
        };
    }

    public RetainerPathData BuildRetainerPaths(HeapGraphData graph, string targetObjectId, int maxPaths)
    {
        _logger.LogInformation("Building retainer paths for target object {TargetObjectId} (max {MaxPaths} paths)",
            targetObjectId, maxPaths);

        var paths = BuildRetainerPaths(graph, targetObjectId);

        // Limit number of paths
        if (paths.RetainerChains.Count > maxPaths)
        {
            paths.RetainerChains = paths.RetainerChains.Take(maxPaths).ToList();
        }

        return paths;
    }
}
