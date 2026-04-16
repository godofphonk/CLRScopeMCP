using ClrScope.Mcp.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Preflight validation for .nettrace files to check if they contain heap snapshot events.
/// This prevents wasting time on traces that don't have the required heap data.
/// </summary>
public sealed class NettracePreflight
{
    private readonly ICliCommandRunner _cliRunner;
    private readonly ILogger<NettracePreflight> _logger;
    private readonly string _heapParserPath;

    public NettracePreflight(
        ICliCommandRunner cliRunner,
        ILogger<NettracePreflight> logger)
    {
        _cliRunner = cliRunner ?? throw new ArgumentNullException(nameof(cliRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _heapParserPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ClrScope.HeapParser.dll");
    }

    public async Task<NettracePreflightResult> CheckAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running nettrace preflight check for {FilePath}", filePath);

        if (!File.Exists(_heapParserPath))
        {
            _logger.LogError("Heap parser not found at {HeapParserPath}", _heapParserPath);
            return NettracePreflightResult.Error("Heap parser not found");
        }

        try
        {
            var args = new[] { _heapParserPath, "probe-nettrace", filePath };
            var result = await _cliRunner.ExecuteAsync("dotnet", args, cancellationToken);

            if (result.ExitCode != 0)
            {
                _logger.LogError("Probe failed with exit code {ExitCode}: {Error}", result.ExitCode, result.StandardError);
                return NettracePreflightResult.Error($"Probe failed: {result.StandardError}");
            }

            var probeResult = JsonSerializer.Deserialize<NettraceProbeResult>(
                result.StandardOutput,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (probeResult == null)
            {
                _logger.LogError("Failed to deserialize probe result");
                return NettracePreflightResult.Error("Failed to parse probe result");
            }

            _logger.LogInformation(
                "Probe result: HasHeapSnapshot={HasHeap}, Mode={Mode}, Message={Message}",
                probeResult.HasHeapSnapshotEvents,
                probeResult.Mode,
                probeResult.Message);

            return new NettracePreflightResult
            {
                IsHeapCapable = probeResult.HasHeapSnapshotEvents,
                IsFullHeapGraphCapable = probeResult.IsFullHeapGraphCapable,
                Mode = probeResult.Mode,
                Message = probeResult.Message,
                NodeCount = probeResult.HasHeapSnapshotEvents ? ExtractNodeCount(probeResult.Message) : 0,
                RecommendedViews = probeResult.RecommendedViews,
                UnsupportedViews = probeResult.UnsupportedViews
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nettrace preflight check failed");
            return NettracePreflightResult.Error($"Preflight check failed: {ex.Message}");
        }
    }

    private static int ExtractNodeCount(string message)
    {
        var match = System.Text.RegularExpressions.Regex.Match(message, @"(\d+) nodes");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
        {
            return count;
        }
        return 0;
    }
}

/// <summary>
/// Result of nettrace preflight check.
/// </summary>
public sealed record NettracePreflightResult
{
    public bool IsHeapCapable { get; init; }
    public bool IsFullHeapGraphCapable { get; init; }
    public string Mode { get; init; } = "no-heap-data"; // no-heap-data, partial-heap-data, full-heap-graph
    public string Message { get; init; } = string.Empty;
    public int NodeCount { get; init; }
    public List<string> RecommendedViews { get; init; } = new();
    public List<string> UnsupportedViews { get; init; } = new();
    public bool IsError { get; init; }

    public static NettracePreflightResult Error(string message) => new()
    {
        IsHeapCapable = false,
        IsFullHeapGraphCapable = false,
        Mode = "no-heap-data",
        Message = message,
        NodeCount = 0,
        RecommendedViews = new List<string>(),
        UnsupportedViews = new List<string>(),
        IsError = true
    };
}

/// <summary>
/// Internal result from HeapParser probe-nettrace command.
/// </summary>
internal sealed record NettraceProbeResult
{
    public bool HasHeapSnapshotEvents { get; set; }
    public bool HasGCBulkNode { get; set; }
    public bool HasGCBulkEdge { get; set; }
    public bool HasGCBulkType { get; set; }
    public bool HasGCBulkRoot { get; set; }
    public bool RuntimeProviderSeen { get; set; }
    public bool IsFullHeapGraphCapable { get; set; }
    public string Mode { get; set; } = "no-heap-data";
    public string RecommendedMode { get; set; } = "no-heap-data";
    public List<string> RecommendedViews { get; set; } = new();
    public List<string> UnsupportedViews { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
