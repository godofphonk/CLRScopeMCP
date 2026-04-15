namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Adapter for DotNetHeapInfo from dotnet/diagnostics.
/// </summary>
public sealed class DotNetHeapInfoAdapter
{
    public required IReadOnlyList<HeapSegmentInfo> Segments { get; init; }

    public static DotNetHeapInfoAdapter From(dynamic heapInfo)
    {
        var segments = new List<HeapSegmentInfo>();

        if (heapInfo?.Segments != null)
        {
            foreach (var s in heapInfo.Segments)
            {
                segments.Add(new HeapSegmentInfo
                {
                    Start = (ulong)s.Start,
                    End = (ulong)s.End,
                    Gen0End = (ulong)s.Gen0End,
                    Gen1End = (ulong)s.Gen1End,
                    Gen2End = (ulong)s.Gen2End,
                    Gen3End = (ulong)s.Gen3End,
                    Gen4End = (ulong)s.Gen4End
                });
            }
        }

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
