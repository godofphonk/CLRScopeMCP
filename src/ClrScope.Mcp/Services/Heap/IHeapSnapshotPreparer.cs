using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Domain.Artifacts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Interface for preparing heap snapshot data from GcDump artifacts.
/// </summary>
public interface IHeapSnapshotPreparer
{
    Task<PreparedHeapVisualizationData> PrepareAsync(
        Artifact artifact,
        HeapPreparationOptions options,
        CancellationToken cancellationToken,
        IProgress<HeapPreparationProgress>? progress = null);
}

/// <summary>
/// Progress information during heap snapshot preparation.
/// </summary>
public sealed class HeapPreparationProgress
{
    public string Phase { get; init; } = string.Empty;
    public int CurrentStep { get; init; }
    public int TotalSteps { get; init; }
    public string Message { get; init; } = string.Empty;
}
