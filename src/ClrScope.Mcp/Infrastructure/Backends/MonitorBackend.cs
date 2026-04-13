using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// dotnet-monitor backend implementation using REST API
/// </summary>
public class MonitorBackend : IMonitorBackend
{
    private readonly ILogger<MonitorBackend> _logger;
    private readonly HttpClient _httpClient;
    private readonly CorrelationIdProvider _correlationIdProvider;
    private readonly string _baseUrl;
    private readonly bool _enabled;

    public MonitorBackend(
        ILogger<MonitorBackend> logger,
        HttpClient httpClient,
        CorrelationIdProvider correlationIdProvider)
    {
        _logger = logger;
        _httpClient = httpClient;
        _correlationIdProvider = correlationIdProvider;
        
        // dotnet-monitor default URL: http://localhost:52323
        var monitorUrl = Environment.GetEnvironmentVariable("CLRSCOPE_MONITOR_URL") ?? "http://localhost:52323";
        _baseUrl = monitorUrl.TrimEnd('/');
        _enabled = !string.IsNullOrEmpty(monitorUrl);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            return false;
        }

        try
        {
            var correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogInformation("[{CorrelationId}] Checking dotnet-monitor availability at {BaseUrl}", correlationId, _baseUrl);

            var response = await _httpClient.GetAsync($"{_baseUrl}/processes", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "dotnet-monitor not available at {BaseUrl}", _baseUrl);
            return false;
        }
    }

    public async Task<MonitorResult> StartTraceAsync(
        int pid,
        TimeSpan duration,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        _logger.LogInformation("[{CorrelationId}] Starting trace capture via dotnet-monitor for PID {Pid}", correlationId, pid);

        try
        {
            // Check if monitor is available
            if (!await IsAvailableAsync(cancellationToken))
            {
                return MonitorResult.FailureResult("dotnet-monitor not available");
            }

            // Request trace capture
            var request = new
            {
                pid = pid,
                durationSeconds = (int)duration.TotalSeconds,
                configuration = new
                {
                    providers = new[]
                    {
                        new { name = "Microsoft-Windows-DotNETRuntime", level = "Informational" }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/trace", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Wait for capture to complete
            await Task.Delay(duration, cancellationToken);

            // Download trace file
            var traceResponse = await _httpClient.GetAsync($"{_baseUrl}/trace/{pid}/download", cancellationToken);
            traceResponse.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            await traceResponse.Content.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("[{CorrelationId}] Trace capture completed for PID {Pid}", correlationId, pid);
            return MonitorResult.SuccessResult(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Trace capture failed for PID {Pid}", correlationId, pid);
            return MonitorResult.FailureResult($"Trace capture failed: {ex.Message}");
        }
    }

    public async Task<MonitorResult> CaptureDumpAsync(
        int pid,
        bool includeHeap,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        _logger.LogInformation("[{CorrelationId}] Capturing dump via dotnet-monitor for PID {Pid}", correlationId, pid);

        try
        {
            // Check if monitor is available
            if (!await IsAvailableAsync(cancellationToken))
            {
                return MonitorResult.FailureResult("dotnet-monitor not available");
            }

            // Request dump capture
            var request = new
            {
                pid = pid,
                type = includeHeap ? "full" : "mini"
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/dump", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Download dump file
            var dumpResponse = await _httpClient.GetAsync($"{_baseUrl}/dump/{pid}/download", cancellationToken);
            dumpResponse.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            await dumpResponse.Content.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("[{CorrelationId}] Dump capture completed for PID {Pid}", correlationId, pid);
            return MonitorResult.SuccessResult(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Dump capture failed for PID {Pid}", correlationId, pid);
            return MonitorResult.FailureResult($"Dump capture failed: {ex.Message}");
        }
    }
}
