using ClrScope.Mcp.Services.Heap;

namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Envelope wrapping MemoryGraph from dotnet/diagnostics with metadata.
/// </summary>
public sealed class MemoryGraphEnvelope
{
    public required object MemoryGraph { get; init; }
    public required DotNetHeapInfoAdapter HeapInfo { get; init; }
    public required HeapCollectionMetadata Metadata { get; init; }
}

/// <summary>
/// Metadata about heap collection.
/// </summary>
public sealed class HeapCollectionMetadata
{
    public string SourceKind { get; init; } = string.Empty;
    public string RuntimeVersion { get; init; } = string.Empty;
    public string ToolVersion { get; init; } = string.Empty;
    public bool IsPartial { get; init; }
    public string? Warning { get; init; }
}

/// <summary>
/// Interface for reading heap graph from various sources.
/// </summary>
public interface IHeapGraphSourceAdapter
{
    Task<MemoryGraphEnvelope> ReadAsync(
        string filePath,
        CancellationToken cancellationToken,
        IProgress<HeapPreparationProgress>? progress = null);
}
