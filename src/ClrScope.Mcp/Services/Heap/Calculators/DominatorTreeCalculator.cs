using ClrScope.Mcp.Domain.Heap.Data;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Calculator for dominator tree and retained size using Lengauer-Tarjan algorithm.
/// </summary>
public sealed class DominatorTreeCalculator
{
    private readonly ILogger<DominatorTreeCalculator> _logger;
    private const long SuperRootNodeId = -1;

    public DominatorTreeCalculator(ILogger<DominatorTreeCalculator> logger)
    {
        _logger = logger;
    }

    public void CalculateRetainedSize(HeapGraphData graph)
    {
        _logger.LogInformation("Calculating retained size for {NodeCount} nodes", graph.Nodes.Count);

        // Phase 1: Lengauer–Tarjan (Dominator Tree)
        var adjacencyList = BuildSuperRoot(graph);
        var filteredAdjacency = FilterWeakEdges(adjacencyList, graph.Edges);
        var immediateDominators = ComputeImmediateDominators(filteredAdjacency, graph.Nodes);
        FillDominatorNodeId(graph.Nodes, immediateDominators);
        AggregateRetainedSize(graph.Nodes, immediateDominators);
    }

    /// <summary>
    /// Phase 2: Find retainer paths for a target node.
    /// </summary>
    public List<RetainerPath> FindRetainerPaths(HeapGraphData graph, long targetNodeId, int maxPaths = 10)
    {
        _logger.LogInformation("Finding retainer paths for target node {TargetNodeId}", targetNodeId);

        // Step 7: Reverse BFS
        var paths = ReverseBFS(graph, targetNodeId);

        // Step 8: Deduplicate paths
        var deduplicatedPaths = DeduplicatePaths(paths);

        // Step 9: Sort and return top N
        var sortedPaths = SortPaths(deduplicatedPaths, graph.Nodes);
        return sortedPaths.Take(maxPaths).ToList();
    }

    /// <summary>
    /// Step 1: BuildSuperRoot - Create virtual super-root (NodeID = -1) and add edges to all root nodes.
    /// </summary>
    private Dictionary<long, List<long>> BuildSuperRoot(HeapGraphData graph)
    {
        var adjacencyList = new Dictionary<long, List<long>>();

        // Initialize adjacency list for all nodes
        foreach (var node in graph.Nodes.Values)
        {
            adjacencyList[node.NodeId] = new List<long>();
        }

        // Add super-root
        adjacencyList[SuperRootNodeId] = new List<long>();

        // Add edges from super-root to all root nodes
        foreach (var node in graph.Nodes.Values.Where(n => n.IsRoot))
        {
            adjacencyList[SuperRootNodeId].Add(node.NodeId);
        }

        // Build original adjacency list from edges
        foreach (var edge in graph.Edges)
        {
            if (!adjacencyList.ContainsKey(edge.FromNodeId))
            {
                adjacencyList[edge.FromNodeId] = new List<long>();
            }
            adjacencyList[edge.FromNodeId].Add(edge.ToNodeId);
        }

        return adjacencyList;
    }

    /// <summary>
    /// Step 2: FilterWeakEdges - Exclude all edges with IsWeak = true from reachability graph.
    /// </summary>
    private Dictionary<long, List<long>> FilterWeakEdges(
        Dictionary<long, List<long>> adjacencyList,
        List<MemoryEdgeData> edges)
    {
        // Build weak edge set
        var weakEdges = new HashSet<(long, long)>();
        foreach (var edge in edges.Where(e => e.IsWeak))
        {
            weakEdges.Add((edge.FromNodeId, edge.ToNodeId));
        }

        // Filter adjacency list
        var filteredAdjacency = new Dictionary<long, List<long>>();
        foreach (var (fromNodeId, toNodeIds) in adjacencyList)
        {
            var filteredToNodeIds = toNodeIds
                .Where(toNodeId => !weakEdges.Contains((fromNodeId, toNodeId)))
                .ToList();

            filteredAdjacency[fromNodeId] = filteredToNodeIds;
        }

        _logger.LogInformation("Filtered {WeakEdgeCount} weak edges from adjacency list", weakEdges.Count);

        return filteredAdjacency;
    }

    /// <summary>
    /// Step 3: ComputeImmediateDominators - Implement Lengauer-Tarjan algorithm.
    /// </summary>
    private Dictionary<long, long?> ComputeImmediateDominators(
        Dictionary<long, List<long>> adjacencyList,
        Dictionary<long, MemoryNodeData> nodes)
    {
        var allNodes = new HashSet<long>(adjacencyList.Keys);
        var semi = new Dictionary<long, long>();
        var vertex = new Dictionary<long, long>(); // DFS numbering
        var parent = new Dictionary<long, long>();
        var bucket = new Dictionary<long, List<long>>();
        var dom = new Dictionary<long, long?>();
        var ancestor = new Dictionary<long, long>();
        var label = new Dictionary<long, long>();
        var dfsNumber = 0;

        // Initialize data structures
        foreach (var nodeId in allNodes)
        {
            semi[nodeId] = nodeId;
            dom[nodeId] = null;
            ancestor[nodeId] = nodeId;
            label[nodeId] = nodeId;
            bucket[nodeId] = new List<long>();
        }

        // DFS from super-root
        void DFS(long u)
        {
            vertex[u] = dfsNumber++;
            foreach (var v in adjacencyList.GetValueOrDefault(u, new List<long>()))
            {
                if (!vertex.ContainsKey(v))
                {
                    parent[v] = u;
                    DFS(v);
                }
            }
        }

        DFS(SuperRootNodeId);

        // Union-Find with path compression
        long Find(long v)
        {
            if (ancestor[v] != v)
            {
                var u = Find(ancestor[v]);
                if (vertex[label[v]] > vertex[label[ancestor[v]]])
                {
                    label[v] = label[ancestor[v]];
                }
                ancestor[v] = u;
            }
            return label[v];
        }

        // Link for union-find
        void Link(long u, long v)
        {
            ancestor[v] = u;
        }

        // Compute semi-dominators
        foreach (var w in allNodes.OrderByDescending(n => vertex.GetValueOrDefault(n, long.MaxValue)))
        {
            if (w == SuperRootNodeId) continue;
            if (!parent.ContainsKey(w)) continue; // Skip nodes without parent (not visited in DFS)

            foreach (var v in adjacencyList.GetValueOrDefault(w, new List<long>()))
            {
                if (vertex.ContainsKey(v))
                {
                    var u = Find(v);
                    if (vertex[semi[w]] > vertex[u])
                    {
                        semi[w] = u;
                    }
                }
            }

            // Ensure bucket is initialized for semi[w]
            if (!bucket.ContainsKey(semi[w]))
            {
                bucket[semi[w]] = new List<long>();
            }
            bucket[semi[w]].Add(w);

            Link(parent[w], w);

            foreach (var v in bucket.GetValueOrDefault(parent[w], new List<long>()))
            {
                var u = Find(v);
                dom[v] = vertex[u] < vertex[v] ? u : parent[w];
            }

            if (!bucket.ContainsKey(parent[w]))
            {
                _logger.LogWarning("Bucket does not contain key {ParentKey} for node {Node}", parent[w], w);
            }
            else
            {
                bucket[parent[w]].Clear();
            }
        }

        // Set immediate dominator for super-root
        dom[SuperRootNodeId] = null;

        return dom;
    }

    /// <summary>
    /// Step 4: FillDominatorNodeId - Fill MemoryNodeData.DominatorNodeId with LT results.
    /// </summary>
    private void FillDominatorNodeId(
        Dictionary<long, MemoryNodeData> nodes,
        Dictionary<long, long?> immediateDominators)
    {
        foreach (var node in nodes.Values)
        {
            node.DominatorNodeId = immediateDominators.GetValueOrDefault(node.NodeId);
        }
    }

    /// <summary>
    /// Step 5: AggregateRetainedSize - Build children map and traverse tree bottom-up.
    /// </summary>
    private void AggregateRetainedSize(
        Dictionary<long, MemoryNodeData> nodes,
        Dictionary<long, long?> immediateDominators)
    {
        // Build children map from dominator tree
        var children = new Dictionary<long, List<long>>();
        foreach (var node in nodes.Values)
        {
            children[node.NodeId] = new List<long>();
        }

        foreach (var (nodeId, dominatorId) in immediateDominators)
        {
            if (dominatorId.HasValue && dominatorId != SuperRootNodeId)
            {
                if (!children.ContainsKey(dominatorId.Value))
                {
                    children[dominatorId.Value] = new List<long>();
                }
                children[dominatorId.Value].Add(nodeId);
            }
        }

        // Post-order traversal (bottom-up) to aggregate retained size
        void PostOrderAggregate(long nodeId, HashSet<long> visited)
        {
            if (visited.Contains(nodeId)) return;
            visited.Add(nodeId);

            if (!nodes.ContainsKey(nodeId)) return;

            var node = nodes[nodeId];
            node.RetainedSizeBytes = node.ShallowSizeBytes;

            foreach (var childId in children.GetValueOrDefault(nodeId, new List<long>()))
            {
                PostOrderAggregate(childId, visited);
                if (nodes.ContainsKey(childId))
                {
                    node.RetainedSizeBytes += nodes[childId].RetainedSizeBytes;
                }
            }
        }

        var visited = new HashSet<long>();
        foreach (var nodeId in nodes.Keys)
        {
            PostOrderAggregate(nodeId, visited);
        }
    }

    /// <summary>
    /// Step 7: Reverse BFS - Build reverse adjacency on strong edges, BFS from target to roots, save paths.
    /// </summary>
    private List<RetainerPath> ReverseBFS(HeapGraphData graph, long targetNodeId)
    {
        _logger.LogInformation("ReverseBFS: Starting for target node {TargetNodeId}", targetNodeId);
        _logger.LogInformation("ReverseBFS: Graph has {NodeCount} nodes, {EdgeCount} edges",
            graph.Nodes.Count, graph.Edges.Count);

        // Check if target node exists
        if (!graph.Nodes.ContainsKey(targetNodeId))
        {
            _logger.LogWarning("ReverseBFS: Target node {TargetNodeId} not found in graph", targetNodeId);
            return new List<RetainerPath>();
        }

        var targetNode = graph.Nodes[targetNodeId];
        _logger.LogInformation("ReverseBFS: Target node {TargetNodeId} is TypeName={TypeName}, IsRoot={IsRoot}",
            targetNodeId, targetNode.TypeName, targetNode.IsRoot);

        // Count root nodes
        var rootCount = graph.Nodes.Values.Count(n => n.IsRoot);
        _logger.LogInformation("ReverseBFS: Graph has {RootCount} root nodes", rootCount);

        // Build reverse adjacency list (only strong edges)
        var reverseAdjacency = new Dictionary<long, List<(long fromNodeId, string edgeKind)>>();
        foreach (var node in graph.Nodes.Values)
        {
            reverseAdjacency[node.NodeId] = new List<(long, string)>();
        }

        foreach (var edge in graph.Edges.Where(e => !e.IsWeak))
        {
            // Only if both nodes exist in graph
            if (graph.Nodes.ContainsKey(edge.FromNodeId) && graph.Nodes.ContainsKey(edge.ToNodeId))
            {
                if (!reverseAdjacency.ContainsKey(edge.ToNodeId))
                {
                    reverseAdjacency[edge.ToNodeId] = new List<(long, string)>();
                }
                reverseAdjacency[edge.ToNodeId].Add((edge.FromNodeId, edge.EdgeKind));
            }
        }

        _logger.LogInformation("ReverseBFS: Built reverse adjacency with {EdgeCount} strong edges",
            graph.Edges.Count(e => !e.IsWeak));

        // Check if target node has predecessors
        var predecessors = reverseAdjacency.GetValueOrDefault(targetNodeId, new List<(long, string)>());
        _logger.LogInformation("ReverseBFS: Target node {TargetNodeId} has {PredCount} predecessors",
            targetNodeId, predecessors.Count);

        // BFS from target to roots
        var paths = new List<RetainerPath>();
        var queue = new Queue<(long nodeId, List<RetainerPathStep> currentPath)>();
        queue.Enqueue((targetNodeId, new List<RetainerPathStep>()));
        var visited = new HashSet<long>();

        _logger.LogInformation("ReverseBFS: Starting BFS from target node {TargetNodeId}", targetNodeId);
        var bfsStep = 0;

        while (queue.Count > 0)
        {
            var (currentNodeId, currentPath) = queue.Dequeue();
            bfsStep++;

            if (bfsStep <= 10) // Log first 10 steps only
            {
                _logger.LogInformation("ReverseBFS: BFS step {Step}, queue size {QueueSize}, currentNodeId {NodeId}, path length {PathLength}",
                    bfsStep, queue.Count, currentNodeId, currentPath.Count);
            }

            // Check if reached a root
            if (graph.Nodes.TryGetValue(currentNodeId, out var currentNode) && currentNode.IsRoot)
            {
                _logger.LogInformation("ReverseBFS: Found root at node {NodeId}, path length {PathLength}", currentNodeId, currentPath.Count);
                paths.Add(new RetainerPath
                {
                    RootNodeId = currentNodeId,
                    RootKind = currentNode.RootKind ?? "Unknown",
                    Steps = currentPath.ToList(),
                    TotalSteps = currentPath.Count
                });
                continue;
            }

            // Explore predecessors
            foreach (var (predNodeId, edgeKind) in reverseAdjacency.GetValueOrDefault(currentNodeId, new List<(long, string)>()))
            {
                if (!visited.Contains(predNodeId) && currentPath.Count < 50)
                {
                    visited.Add(predNodeId);

                    var newPath = currentPath.ToList();
                    newPath.Add(new RetainerPathStep
                    {
                        FromNodeId = predNodeId,
                        ToNodeId = currentNodeId,
                        EdgeKind = edgeKind,
                        IsWeak = false // All edges in reverse adjacency are strong edges
                    });

                    queue.Enqueue((predNodeId, newPath));
                }
                else if (bfsStep <= 10)
                {
                    _logger.LogInformation("ReverseBFS: Skipping predecessor {PredNodeId} - visited: {Visited}, path too long: {TooLong}",
                        predNodeId, visited.Contains(predNodeId), currentPath.Count >= 50);
                }
            }
        }

        _logger.LogInformation("ReverseBFS: BFS completed in {Steps} steps, found {PathCount} paths", bfsStep, paths.Count);
        return paths;
    }

    /// <summary>
    /// Step 8: Deduplicate paths - Key: (ObjectId, FieldName) sequence.
    /// </summary>
    private List<RetainerPath> DeduplicatePaths(List<RetainerPath> paths)
    {
        var seen = new HashSet<string>();
        var deduplicated = new List<RetainerPath>();

        foreach (var path in paths)
        {
            // Create deduplication key from sequence of (NodeId, EdgeKind)
            var key = string.Join("|", path.Steps.Select(s => $"{s.FromNodeId}:{s.EdgeKind}"));

            if (!seen.Contains(key))
            {
                seen.Add(key);
                deduplicated.Add(path);
            }
        }

        _logger.LogInformation("Deduplicated {OriginalCount} paths to {DeduplicatedCount}", paths.Count, deduplicated.Count);
        return deduplicated;
    }

    /// <summary>
    /// Step 9: Sort paths - Priority: shorter → non-weak → larger retaining node, return top N.
    /// </summary>
    private List<RetainerPath> SortPaths(List<RetainerPath> paths, Dictionary<long, MemoryNodeData> nodes)
    {
        var sorted = paths.OrderByDescending(p =>
        {
            // Priority 1: Shorter paths first
            var lengthScore = -p.TotalSteps;

            // Priority 2: Non-weak edges preferred
            var weakScore = -p.Steps.Count(s => s.IsWeak);

            // Priority 3: Maximum retained size in the path
            var maxRetainedSizeInPath = 0L;
            if (p.Steps.Count > 0)
            {
                maxRetainedSizeInPath = p.Steps
                    .Select(step => nodes.TryGetValue(step.FromNodeId, out var node) ? node.RetainedSizeBytes : 0L)
                    .Max();
            }
            var sizeScore = maxRetainedSizeInPath;

            // Composite score
            return (lengthScore * 10000) + (weakScore * 100) + (sizeScore / 1024); // Normalize size to KB
        }).ToList();

        return sorted;
    }
}

/// <summary>
/// Represents a retainer path from root to target node.
/// </summary>
public sealed class RetainerPath
{
    public long RootNodeId { get; set; }
    public string RootKind { get; set; } = string.Empty;
    public List<RetainerPathStep> Steps { get; set; } = new();
    public int TotalSteps { get; set; }
}

/// <summary>
/// Represents a single step in a retainer path.
/// </summary>
public sealed class RetainerPathStep
{
    public long FromNodeId { get; set; }
    public long ToNodeId { get; set; }
    public string EdgeKind { get; set; } = string.Empty;
    public bool IsWeak { get; set; }
}
