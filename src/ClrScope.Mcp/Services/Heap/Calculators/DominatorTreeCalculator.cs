using ClrScope.Mcp.Domain.Heap.Data;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Calculator for dominator tree and retained size using Cooper-Harvey-Kennedy algorithm.
/// Iterative reverse-postorder dataflow approach - simpler and more reliable than Lengauer-Tarjan.
/// </summary>
public sealed class DominatorTreeCalculator
{
    private readonly ILogger<DominatorTreeCalculator> _logger;
    private const long SuperRootNodeId = -1;
    private const int MaxRetainerPathLength = 50;

    public DominatorTreeCalculator(ILogger<DominatorTreeCalculator> logger)
    {
        _logger = logger;
    }

    public void CalculateRetainedSize(HeapGraphData graph)
    {
        _logger.LogInformation("CalculateRetainedSize: Starting with {NodeCount} nodes, {EdgeCount} edges", 
            graph.Nodes.Count, graph.Edges.Count);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var adjacencyList = BuildSuperRoot(graph);
        _logger.LogInformation("CalculateRetainedSize: BuildSuperRoot completed in {Ms}ms", stopwatch.ElapsedMilliseconds);

        var filteredAdjacency = FilterWeakEdges(adjacencyList, graph.Edges);
        _logger.LogInformation("CalculateRetainedSize: FilterWeakEdges completed in {Ms}ms", stopwatch.ElapsedMilliseconds);

        var immediateDominators = ComputeImmediateDominatorsCHK(filteredAdjacency, graph.Nodes);
        _logger.LogInformation("CalculateRetainedSize: ComputeImmediateDominatorsCHK completed in {Ms}ms", stopwatch.ElapsedMilliseconds);

        FillDominatorNodeId(graph.Nodes, immediateDominators);
        _logger.LogInformation("CalculateRetainedSize: FillDominatorNodeId completed in {Ms}ms", stopwatch.ElapsedMilliseconds);

        AggregateRetainedSize(graph.Nodes, immediateDominators);
        _logger.LogInformation("CalculateRetainedSize: AggregateRetainedSize completed in {Ms}ms", stopwatch.ElapsedMilliseconds);

        stopwatch.Stop();
        _logger.LogInformation("CalculateRetainedSize: Total time {TotalMs}ms", stopwatch.ElapsedMilliseconds);
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
        var rootNodes = graph.Nodes.Values.Where(n => n.IsRoot).ToList();
        foreach (var node in rootNodes)
        {
            adjacencyList[SuperRootNodeId].Add(node.NodeId);
        }

        // Build original adjacency list from edges
        var weakEdgeCount = 0;
        int filteredEdges = 0;
        foreach (var edge in graph.Edges)
        {
            // Filter dangling references (toNodeId not in adjacencyList)
            if (!adjacencyList.ContainsKey(edge.ToNodeId))
            {
                filteredEdges++;
                continue;
            }

            if (!adjacencyList.ContainsKey(edge.FromNodeId))
            {
                adjacencyList[edge.FromNodeId] = new List<long>();
            }
            adjacencyList[edge.FromNodeId].Add(edge.ToNodeId);
            if (edge.IsWeak) weakEdgeCount++;
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
    /// Step 3: ComputeImmediateDominators - Cooper-Harvey-Kennedy algorithm (iterative reverse-postorder).
    /// Based on "A Simple, Fast Dominance Algorithm" by Cooper, Harvey, Kennedy (2001).
    /// </summary>
    private Dictionary<long, long?> ComputeImmediateDominatorsCHK(
        Dictionary<long, List<long>> adjacencyList,
        Dictionary<long, MemoryNodeData> nodes)
    {
        // Step 3a: Postorder DFS numbering from super-root
        var postorder = new Dictionary<long, long>();
        var postorderList = new List<long>(); // nodes in postorder
        var visited = new HashSet<long>();
        long postNum = 0;

        void PostorderDFS(long u)
        {
            var stack = new Stack<(long node, int childIndex)>();
            visited.Add(u);
            stack.Push((u, 0));

            while (stack.Count > 0)
            {
                var (node, childIdx) = stack.Pop();
                var children = adjacencyList.GetValueOrDefault(node, new List<long>());

                if (childIdx < children.Count)
                {
                    stack.Push((node, childIdx + 1));
                    var child = children[childIdx];
                    if (!visited.Contains(child))
                    {
                        visited.Add(child);
                        stack.Push((child, 0));
                    }
                }
                else
                {
                    postorder[node] = postNum++;
                    postorderList.Add(node);
                }
            }
        }

        PostorderDFS(SuperRootNodeId);

        _logger.LogInformation("CHK: DFS visited {VisitedCount} nodes out of {TotalCount} adjacency entries",
            visited.Count, adjacencyList.Count);

        // Build reverse adjacency list (predecessors) - only for visited nodes
        var reverseAdjacency = new Dictionary<long, List<long>>();
        foreach (var nodeId in visited)
        {
            reverseAdjacency[nodeId] = new List<long>();
        }
        foreach (var (from, toList) in adjacencyList)
        {
            if (!visited.Contains(from)) continue;
            foreach (var to in toList)
            {
                if (!visited.Contains(to)) continue;
                reverseAdjacency[to].Add(from);
            }
        }

        // Initialize dom array
        var dom = new Dictionary<long, long?>();
        foreach (var nodeId in visited)
        {
            dom[nodeId] = null;
        }
        dom[SuperRootNodeId] = SuperRootNodeId;

        // Intersect function using postorder numbering
        // Lower postorder number = further from root, walk UP to idom (higher number)
        long Intersect(long b1, long b2)
        {
            var finger1 = b1;
            var finger2 = b2;
            int safety = visited.Count + 1;
            while (finger1 != finger2 && safety-- > 0)
            {
                while (postorder[finger1] < postorder[finger2] && safety-- > 0)
                {
                    finger1 = dom[finger1]!.Value;
                }
                while (postorder[finger2] < postorder[finger1] && safety-- > 0)
                {
                    finger2 = dom[finger2]!.Value;
                }
            }
            return finger1;
        }

        // Process nodes in reverse postorder (highest postorder first, skip super-root)
        var reversePostorder = new List<long>(postorderList);
        reversePostorder.Reverse();

        _logger.LogInformation("CHK: Processing {NodeCount} nodes in reverse postorder", reversePostorder.Count);

        bool changed;
        int iteration = 0;
        do
        {
            changed = false;
            iteration++;

            foreach (var b in reversePostorder)
            {
                if (b == SuperRootNodeId) continue;

                var preds = reverseAdjacency.GetValueOrDefault(b, new List<long>());
                if (preds.Count == 0) continue;

                // Pick first processed predecessor (one with dom != null)
                long newIdom = -2; // sentinel
                int startIdx = 0;
                for (int i = 0; i < preds.Count; i++)
                {
                    if (dom[preds[i]] != null)
                    {
                        newIdom = preds[i];
                        startIdx = i + 1;
                        break;
                    }
                }
                if (newIdom == -2) continue; // no processed predecessor yet

                // Intersect with remaining processed predecessors
                for (int i = startIdx; i < preds.Count; i++)
                {
                    if (dom[preds[i]] != null)
                    {
                        newIdom = Intersect(preds[i], newIdom);
                    }
                }

                if (dom[b] != newIdom)
                {
                    dom[b] = newIdom;
                    changed = true;
                }
            }
        } while (changed);

        _logger.LogInformation("CHK: Completed dominator computation in {Iterations} iterations", iteration);

        // Super-root has no dominator (clear it for external use)
        dom[SuperRootNodeId] = null;

        // Also add entries for unreachable nodes
        foreach (var nodeId in adjacencyList.Keys)
        {
            if (!dom.ContainsKey(nodeId))
            {
                dom[nodeId] = null;
            }
        }

        return dom;
    }

    /// <summary>
    /// Step 4: FillDominatorNodeId - Fill MemoryNodeData.DominatorNodeId with CHK results.
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

        // Post-order traversal (bottom-up) to aggregate retained size - iterative to prevent StackOverflow
        var visited = new HashSet<long>();
        foreach (var nodeId in nodes.Keys)
        {
            if (visited.Contains(nodeId)) continue;

            // Iterative post-order DFS using explicit stack
            var stack = new Stack<(long node, int state)>();
            stack.Push((nodeId, 0)); // state 0 = visit children, state 1 = aggregate

            while (stack.Count > 0)
            {
                var (currentNode, state) = stack.Pop();

                if (state == 0)
                {
                    if (visited.Contains(currentNode)) continue;
                    visited.Add(currentNode);

                    if (!nodes.ContainsKey(currentNode)) continue;

                    // Push aggregation phase
                    stack.Push((currentNode, 1));

                    // Push children in reverse order to process them in order
                    var childList = children.GetValueOrDefault(currentNode, new List<long>());
                    for (int i = childList.Count - 1; i >= 0; i--)
                    {
                        stack.Push((childList[i], 0));
                    }
                }
                else
                {
                    // Aggregation phase
                    if (!nodes.ContainsKey(currentNode)) continue;

                    var node = nodes[currentNode];
                    node.RetainedSizeBytes = node.ShallowSizeBytes;

                    foreach (var childId in children.GetValueOrDefault(currentNode, new List<long>()))
                    {
                        if (nodes.ContainsKey(childId))
                        {
                            node.RetainedSizeBytes += nodes[childId].RetainedSizeBytes;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Step 7: Reverse BFS - Build reverse adjacency on strong edges, BFS from target to roots, save paths.
    /// Uses per-path visited set to find all possible paths, not just one.
    /// </summary>
    private List<RetainerPath> ReverseBFS(HeapGraphData graph, long targetNodeId)
    {
        _logger.LogInformation("ReverseBFS: Starting for target node {TargetNodeId}", targetNodeId);
        _logger.LogDebug("ReverseBFS: Graph has {NodeCount} nodes, {EdgeCount} edges",
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
            reverseAdjacency[edge.ToNodeId].Add((edge.FromNodeId, edge.EdgeKind));
        }

        _logger.LogInformation("ReverseBFS: Built reverse adjacency with {EdgeCount} strong edges",
            graph.Edges.Count(e => !e.IsWeak));

        // Check if target node has predecessors
        var predecessors = reverseAdjacency.GetValueOrDefault(targetNodeId, new List<(long, string)>());
        _logger.LogInformation("ReverseBFS: Target node {TargetNodeId} has {PredCount} predecessors",
            targetNodeId, predecessors.Count);

        // BFS from target to roots with per-path visited set
        var paths = new List<RetainerPath>();
        var queue = new Queue<(long nodeId, List<RetainerPathStep> currentPath, HashSet<long> visited)>();
        queue.Enqueue((targetNodeId, new List<RetainerPathStep>(), new HashSet<long> { targetNodeId }));

        _logger.LogInformation("ReverseBFS: Starting BFS from target node {TargetNodeId}", targetNodeId);
        var bfsStep = 0;

        while (queue.Count > 0)
        {
            var (currentNodeId, currentPath, visited) = queue.Dequeue();
            bfsStep++;

            if (bfsStep <= 10) // Log first 10 steps only
            {
                _logger.LogDebug("ReverseBFS: BFS step {Step}, queue size {QueueSize}, currentNodeId {NodeId}, path length {PathLength}",
                    bfsStep, queue.Count, currentNodeId, currentPath.Count);
            }

            // Check if reached a root
            if (graph.Nodes.TryGetValue(currentNodeId, out var currentNode) && currentNode.IsRoot)
            {
                _logger.LogDebug("ReverseBFS: Found root at node {NodeId}, path length {PathLength}", currentNodeId, currentPath.Count);
                // Reverse steps to get root->target order
                var reversedSteps = new List<RetainerPathStep>(currentPath);
                reversedSteps.Reverse();
                paths.Add(new RetainerPath
                {
                    RootNodeId = currentNodeId,
                    RootKind = currentNode.RootKind ?? "Unknown",
                    Steps = reversedSteps,
                    TotalSteps = currentPath.Count
                });
                continue;
            }

            // Explore predecessors
            foreach (var (predNodeId, edgeKind) in reverseAdjacency.GetValueOrDefault(currentNodeId, new List<(long, string)>()))
            {
                if (!visited.Contains(predNodeId) && currentPath.Count < MaxRetainerPathLength)
                {
                    var newVisited = new HashSet<long>(visited);
                    newVisited.Add(predNodeId);

                    var newPath = new List<RetainerPathStep>(currentPath);
                    newPath.Add(new RetainerPathStep
                    {
                        FromNodeId = predNodeId,
                        ToNodeId = currentNodeId,
                        EdgeKind = edgeKind,
                        IsWeak = false // All edges in reverse adjacency are strong edges
                    });

                    queue.Enqueue((predNodeId, newPath, newVisited));
                }
                else if (bfsStep <= 10)
                {
                    _logger.LogDebug("ReverseBFS: Skipping predecessor {PredNodeId} - visited: {Visited}, path too long: {TooLong}",
                        predNodeId, visited.Contains(predNodeId), currentPath.Count >= MaxRetainerPathLength);
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
        var sorted = paths
            .OrderBy(p => p.TotalSteps)  // Shorter paths first
            .ThenBy(p => p.Steps.Count(s => s.IsWeak))  // Fewer weak edges preferred
            .ThenByDescending(p =>
            {
                // Maximum retained size in the path
                if (p.Steps.Count == 0) return 0L;
                return p.Steps
                    .Select(step => nodes.TryGetValue(step.FromNodeId, out var node) ? node.RetainedSizeBytes : 0L)
                    .Max();
            })
            .ToList();

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
