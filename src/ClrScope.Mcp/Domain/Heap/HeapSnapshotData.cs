namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Heap snapshot data structure for GcDump artifacts.
/// Simplified version for heapstat mode (dotnet-gcdump report).
/// </summary>
public sealed class HeapSnapshotData
{
    public required Artifact Artifact { get; init; }
    public required HeapMetadata Metadata { get; init; }

    // Aggregated type statistics (from dotnet-gcdump report)
    public required IReadOnlyList<TypeStatData> TypeStats { get; init; }
}

/// <summary>
/// Metadata about the heap snapshot.
/// </summary>
public sealed class HeapMetadata
{
    public string RuntimeVersion { get; init; } = string.Empty;
    public string ToolVersion { get; init; } = string.Empty;
    public long TotalHeapBytes { get; init; }
    public long TotalObjectCount { get; init; }
    public bool IsPartial { get; init; }
    public string? Warning { get; init; }
}

/// <summary>
/// Type-level statistics aggregated from heap snapshot.
/// </summary>
public sealed class TypeStatData
{
    public string TypeName { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public string Generation { get; init; } = "mixed";

    public int Count { get; init; }
    public long ShallowSizeBytes { get; init; }
    public long RetainedSizeBytes { get; init; } // Not available in heapstat mode, will be 0
}
