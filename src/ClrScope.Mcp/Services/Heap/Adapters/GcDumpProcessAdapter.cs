using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// GcDump graph adapter using separate process for proper timeout handling.
/// Runs ClrScope.HeapParser.exe in a separate process with timeout support.
/// </summary>
public sealed class GcDumpProcessAdapter : IGcDumpGraphAdapter
{
    private readonly ILogger<GcDumpProcessAdapter> _logger;
    private readonly string _heapParserPath;

    public GcDumpProcessAdapter(ILogger<GcDumpProcessAdapter> logger)
    {
        _logger = logger;
        // Path to the heap parser executable (dll on Linux, exe on Windows)
        _heapParserPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ClrScope.HeapParser.dll");
    }

    public async Task<HeapGraphData> LoadGraphAsync(string gcdumpPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GcDumpProcessAdapter: Loading heap graph from {GcDumpPath}", gcdumpPath);
        _logger.LogInformation("GcDumpProcessAdapter: Heap parser path: {HeapParserPath}", _heapParserPath);

        if (!File.Exists(_heapParserPath))
        {
            _logger.LogError("GcDumpProcessAdapter: Heap parser not found at: {HeapParserPath}", _heapParserPath);
            throw new FileNotFoundException($"Heap parser not found at: {_heapParserPath}");
        }

        // Run heap parser in separate process with timeout
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = $"\"{_heapParserPath}\" gcdump \"{gcdumpPath}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) => outputBuilder.AppendLine(e.Data);
        process.ErrorDataReceived += (sender, e) => errorBuilder.AppendLine(e.Data);

        _logger.LogInformation("Starting heap parser process");
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for completion with timeout
        var timeoutTask = Task.Run(() => process.WaitForExit(), cancellationToken);
        var timeoutDelay = Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

        var completedTask = await Task.WhenAny(timeoutTask, timeoutDelay);

        if (completedTask == timeoutDelay)
        {
            _logger.LogError("Heap parser timed out after 5 minutes, killing process");
            try
            {
                process.Kill(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill heap parser process");
            }
            throw new TimeoutException("Heap parser timed out after 5 minutes");
        }

        await timeoutTask; // Ensure WaitForExit completes

        if (process.ExitCode != 0)
        {
            _logger.LogError("Heap parser failed with exit code {ExitCode}: {Error}", process.ExitCode, errorBuilder.ToString());
            throw new InvalidOperationException($"Heap parser failed: {errorBuilder}");
        }

        _logger.LogInformation("Heap parser completed successfully");
        
        // Parse JSON output
        var json = outputBuilder.ToString();
        return ParseJson(json);
    }

    public Task<HeapGraphData> LoadGraphAsync(Stream gcdumpStream, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Stream loading not supported by process adapter. Use file path instead.");
    }

    private HeapGraphData ParseJson(string json)
    {
        try
        {
            _logger.LogInformation("Parsing heap parser JSON output");
            var graphData = JsonSerializer.Deserialize<HeapGraphData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (graphData == null)
            {
                throw new InvalidOperationException("Failed to deserialize heap graph data");
            }

            _logger.LogInformation("Parsed {NodeCount} nodes, {EdgeCount} edges, {RootCount} roots", 
                graphData.Nodes.Count, graphData.Edges.Count, graphData.Roots.Count);

            return graphData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse heap parser JSON output");
            throw new InvalidOperationException("Failed to parse heap parser JSON output", ex);
        }
    }
}
