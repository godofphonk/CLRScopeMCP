using ClrScope.Mcp.Domain.Heap;
using Graphs;
using Microsoft.Diagnostics.Tools.GCDump;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Adapter for reading heap graph from EventPipe traces (.nettrace files).
/// Uses vendored EventPipeDotNetHeapDumper from dotnet/diagnostics.
/// </summary>
public sealed class EventPipeHeapGraphSourceAdapter : IHeapGraphSourceAdapter
{
    private readonly ILogger<EventPipeHeapGraphSourceAdapter> _logger;

    public EventPipeHeapGraphSourceAdapter(ILogger<EventPipeHeapGraphSourceAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<MemoryGraphEnvelope> ReadAsync(
        string filePath,
        CancellationToken cancellationToken,
        IProgress<HeapPreparationProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"EventPipe trace file not found: {filePath}", filePath);

        _logger.LogInformation("Reading EventPipe heap graph from {FilePath}", filePath);
        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Reading",
            CurrentStep = 1,
            TotalSteps = 3,
            Message = "Initializing MemoryGraph"
        });

        var memoryGraph = new MemoryGraph(100000);
        var dotNetHeapInfo = new DotNetHeapInfo();

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Reading",
            CurrentStep = 2,
            TotalSteps = 3,
            Message = "Parsing EventPipe trace events"
        });

        await Task.Run(() =>
        {
            using var logWriter = new StringWriter();
            var success = EventPipeDotNetHeapDumper.DumpFromEventPipeFile(
                filePath,
                memoryGraph,
                logWriter,
                dotNetHeapInfo);

            if (!success)
            {
                _logger.LogWarning("EventPipeDumpFromEventPipeFile returned false for {FilePath}", filePath);
                _logger.LogDebug("Dump log: {Log}", logWriter.ToString());
                throw new InvalidOperationException(
                    "Failed to parse EventPipe trace. The trace file may not contain GC heap events. " +
                    "Ensure the trace was collected with the gc-heap profile (e.g., dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0xFFFFFFFFFFFFFFFF:5).");
            }

            // Check if graph has any nodes
            if (memoryGraph.NodeIndexLimit == 0)
            {
                throw new InvalidOperationException(
                    "No heap data found in EventPipe trace. The trace file may not contain GC heap events. " +
                    "Ensure the trace was collected with the gc-heap profile (e.g., dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0xFFFFFFFFFFFFFFFF:5).");
            }
        }, cancellationToken);

        progress?.Report(new HeapPreparationProgress
        {
            Phase = "Reading",
            CurrentStep = 3,
            TotalSteps = 3,
            Message = "Building envelope"
        });

        var segments = dotNetHeapInfo.Segments.Select(s => new HeapSegmentInfo
        {
            Start = s.Start,
            End = s.End,
            Gen0End = s.Gen0End,
            Gen1End = s.Gen1End,
            Gen2End = s.Gen2End,
            Gen3End = s.Gen3End,
            Gen4End = s.Gen4End
        }).ToList();

        return new MemoryGraphEnvelope
        {
            MemoryGraph = memoryGraph,
            HeapInfo = new DotNetHeapInfoAdapter
            {
                Segments = segments
            },
            Metadata = new HeapCollectionMetadata
            {
                SourceKind = "nettrace",
                RuntimeVersion = "unknown",
                ToolVersion = "dotnet-gcdump (vendored)",
                IsPartial = false,
                Warning = null
            }
        };
    }
}
