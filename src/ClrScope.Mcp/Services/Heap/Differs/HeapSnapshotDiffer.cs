using ClrScope.Mcp.Domain.Heap.Data;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Differ for comparing two heap snapshots.
/// </summary>
public sealed class HeapSnapshotDiffer
{
    private readonly ILogger<HeapSnapshotDiffer> _logger;

    public HeapSnapshotDiffer(ILogger<HeapSnapshotDiffer> logger)
    {
        _logger = logger;
    }

    public HeapSnapshotDiffData Diff(HeapSnapshotData baseline, HeapSnapshotData target)
    {
        _logger.LogInformation("Diffing heap snapshots: baseline {BaselineId} vs target {TargetId}",
            baseline.Artifact.ArtifactId.Value, target.Artifact.ArtifactId.Value);

        var baselineTypes = baseline.TypeStats.ToDictionary(t => t.TypeName);
        var targetTypes = target.TypeStats.ToDictionary(t => t.TypeName);

        var allTypes = baselineTypes.Keys.Union(targetTypes.Keys).ToList();
        var typeDiffs = new List<TypeDiffData>();

        foreach (var typeName in allTypes)
        {
            var baselineType = baselineTypes.GetValueOrDefault(typeName);
            var targetType = targetTypes.GetValueOrDefault(typeName);

            var diff = CalculateTypeDiff(typeName, baselineType, targetType);
            typeDiffs.Add(diff);
        }

        // Sort by size delta (descending)
        typeDiffs = typeDiffs.OrderByDescending(d => Math.Abs(d.ShallowSizeDelta)).ToList();

        return new HeapSnapshotDiffData
        {
            Baseline = baseline,
            Target = target,
            TypeDiffs = typeDiffs
        };
    }

    private TypeDiffData CalculateTypeDiff(string typeName, TypeStatData? baseline, TypeStatData? target)
    {
        if (baseline == null && target != null)
        {
            return new TypeDiffData
            {
                TypeName = target.TypeName,
                Namespace = target.Namespace,
                AssemblyName = target.AssemblyName,
                Status = DiffStatus.Added,
                BaselineCount = 0,
                TargetCount = target.Count,
                CountDelta = target.Count,
                BaselineShallowSize = 0,
                TargetShallowSize = target.ShallowSizeBytes,
                ShallowSizeDelta = target.ShallowSizeBytes,
                ShallowSizePercentChange = double.PositiveInfinity
            };
        }

        if (baseline != null && target == null)
        {
            return new TypeDiffData
            {
                TypeName = baseline.TypeName,
                Namespace = baseline.Namespace,
                AssemblyName = baseline.AssemblyName,
                Status = DiffStatus.Removed,
                BaselineCount = baseline.Count,
                TargetCount = 0,
                CountDelta = -baseline.Count,
                BaselineShallowSize = baseline.ShallowSizeBytes,
                TargetShallowSize = 0,
                ShallowSizeDelta = -baseline.ShallowSizeBytes,
                ShallowSizePercentChange = double.NegativeInfinity
            };
        }

        // Both exist
        var countDelta = target!.Count - baseline!.Count;
        var sizeDelta = target.ShallowSizeBytes - baseline.ShallowSizeBytes;
        var percentChange = baseline.ShallowSizeBytes > 0
            ? (sizeDelta / (double)baseline.ShallowSizeBytes) * 100
            : 0;

        var status = DetermineDiffStatus(countDelta, sizeDelta);

        return new TypeDiffData
        {
            TypeName = target.TypeName,
            Namespace = target.Namespace,
            AssemblyName = target.AssemblyName,
            Status = status,
            BaselineCount = baseline.Count,
            TargetCount = target.Count,
            CountDelta = countDelta,
            BaselineShallowSize = baseline.ShallowSizeBytes,
            TargetShallowSize = target.ShallowSizeBytes,
            ShallowSizeDelta = sizeDelta,
            ShallowSizePercentChange = percentChange
        };
    }

    private DiffStatus DetermineDiffStatus(int countDelta, long sizeDelta)
    {
        if (countDelta == 0 && sizeDelta == 0)
            return DiffStatus.Unchanged;

        if (countDelta > 0 && sizeDelta > 0)
            return DiffStatus.Increased;

        if (countDelta < 0 && sizeDelta < 0)
            return DiffStatus.Decreased;

        return DiffStatus.Changed;
    }
}
