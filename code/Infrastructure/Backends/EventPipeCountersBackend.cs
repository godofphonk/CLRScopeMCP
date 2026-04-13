using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Tracing;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Native EventPipe backend for collecting performance counters
/// </summary>
public class EventPipeCountersBackend : ICountersBackend
{
    private readonly ILogger<EventPipeCountersBackend> _logger;
    private readonly CorrelationIdProvider _correlationIdProvider;

    public EventPipeCountersBackend(
        ILogger<EventPipeCountersBackend> logger,
        CorrelationIdProvider correlationIdProvider)
    {
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task<CountersResult> CollectAsync(
        int pid,
        string[] providers,
        TimeSpan duration,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        _logger.LogInformation("[{CorrelationId}] Starting EventPipe counters collection for PID {Pid}", correlationId, pid);

        try
        {
            // Validate process is .NET
            var publishedProcesses = DiagnosticsClient.GetPublishedProcesses();
            if (!publishedProcesses.Contains(pid))
            {
                return CountersResult.FailureResult($"Process with PID {pid} is not a .NET process or not attachable");
            }

            // Create DiagnosticsClient
            var client = new DiagnosticsClient(pid);

            // Build provider list
            var eventPipeProviders = BuildProviders(providers);
            _logger.LogInformation("[{CorrelationId}] Using {ProviderCount} counter providers", correlationId, eventPipeProviders.Count);

            // Start EventPipe session
            using var eventPipeSession = client.StartEventPipeSession(
                eventPipeProviders,
                requestRundown: true,
                circularBufferMB: 256);

            _logger.LogInformation("[{CorrelationId}] EventPipe session started for PID {Pid}", correlationId, pid);

            // Create output file
            await using var fileStream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            // Copy EventPipe stream directly to file (simplified implementation)
            var copyTask = eventPipeSession.EventStream.CopyToAsync(fileStream, cancellationToken);

            // Wait for duration
            await Task.Delay(duration, cancellationToken);

            // Stop session
            try
            {
                await eventPipeSession.StopAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{CorrelationId}] Error stopping EventPipe session", correlationId);
            }

            await fileStream.FlushAsync(CancellationToken.None);

            _logger.LogInformation("[{CorrelationId}] Counter collection completed", correlationId);

            return CountersResult.SuccessResult(outputPath, 0);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[{CorrelationId}] Counter collection cancelled", correlationId);
            return CountersResult.FailureResult("Counter collection cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Counter collection failed for PID {Pid}", correlationId, pid);
            return CountersResult.FailureResult($"Counter collection failed: {ex.Message}");
        }
    }

    private List<EventPipeProvider> BuildProviders(string[] providerNames)
    {
        var providers = new List<EventPipeProvider>();

        foreach (var providerName in providerNames)
        {
            // Parse provider name with optional keywords
            // Format: "System.Runtime" or "System.Runtime:gc-heap-size"
            var parts = providerName.Split(':');
            var name = parts[0];
            var keywords = parts.Length > 1 ? ParseKeywords(parts[1]) : -1L;

            providers.Add(new EventPipeProvider(name, EventLevel.Informational, keywords));
        }

        // Default providers if none specified
        if (providers.Count == 0)
        {
            providers.Add(new EventPipeProvider("System.Runtime", EventLevel.Informational, -1L));
            providers.Add(new EventPipeProvider("Microsoft.AspNetCore.Hosting", EventLevel.Informational, -1L));
        }

        return providers;
    }

    private long ParseKeywords(string keywordString)
    {
        // Simple keyword parsing - could be extended
        return -1L;
    }
}
