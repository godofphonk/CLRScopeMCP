using Microsoft.Diagnostics.Tools.GCDump;

namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Adapter for DotNetHeapInfo from dotnet/diagnostics.
/// </summary>
public sealed class DotNetHeapInfoAdapter
{
    public required IReadOnlyList<HeapSegmentInfo> Segments { get; init; }

    public static DotNetHeapInfoAdapter From(DotNetHeapInfo heapInfo)
    {
        var segments = heapInfo.Segments.Select(s => new HeapSegmentInfo
        {
            Start = s.Start,
            End = s.End,
            Gen0End = s.Gen0End,
            Gen1End = s.Gen1End,
            Gen2End = s.Gen2End,
            Gen3End = s.Gen3End,
            Gen4End = s.Gen4End
        }).ToList();

        return new DotNetHeapInfoAdapter
        {
            Segments = segments
        };
    }
}

/// <summary>
/// Heap segment information from DotNetHeapInfo.
/// </summary>
public sealed class HeapSegmentInfo
{
    public ulong Start { get; init; }
    public ulong End { get; init; }
    public ulong Gen0End { get; init; }
    public ulong Gen1End { get; init; }
    public ulong Gen2End { get; init; }
    public ulong Gen3End { get; init; } // LOH
    public ulong Gen4End { get; init; } // POH / extra generation bucket
}
