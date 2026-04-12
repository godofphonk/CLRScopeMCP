using ClrScope.Mcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools;

[McpServerToolType]
public sealed class CollectTools
{
    private readonly CollectDumpService _dumpService;
    private readonly CollectTraceService _traceService;
    private readonly ILogger<CollectTools> _logger;

    public CollectTools(CollectDumpService dumpService, CollectTraceService traceService, ILogger<CollectTools> logger)
    {
        _dumpService = dumpService;
        _traceService = traceService;
        _logger = logger;
    }

    [McpServerTool(Name = "collect.dump", Title = "Collect Memory Dump", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false), Description("Сбор memory dump из .NET процесса через WriteDump(). Возвращает Session ID и Artifact ID.")]
    public async Task<CollectDumpResult> CollectDump(
        [Description("Process ID to collect dump from")] int pid,
        [Description("Include heap in dump (default: true)")] bool includeHeap = true,
        CancellationToken cancellationToken = default)
    {
        if (pid <= 0)
        {
            throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
        }

        try
        {
            _logger.LogInformation("Starting dump collection for PID {Pid}, IncludeHeap={IncludeHeap}", pid, includeHeap);
            
            var request = new CollectDumpRequest(pid, includeHeap);
            var result = await _dumpService.CollectDumpAsync(request, cancellationToken);
            
            if (result.Artifact != null)
            {
                _logger.LogInformation("Dump collected successfully: SessionId={SessionId}, ArtifactId={ArtifactId}", 
                    result.Session.SessionId.Value, result.Artifact.ArtifactId.Value);
                
                return new CollectDumpResult(
                    Success: true,
                    SessionId: result.Session.SessionId.Value,
                    ArtifactId: result.Artifact.ArtifactId.Value,
                    FilePath: result.Artifact.FilePath,
                    SizeBytes: result.Artifact.SizeBytes,
                    Sha256: result.Artifact.Sha256,
                    Error: null
                );
            }
            else
            {
                _logger.LogWarning("Dump collection failed for PID {Pid}: {Error}", pid, result.Error);
                return new CollectDumpResult(
                    Success: false,
                    SessionId: result.Session.SessionId.Value,
                    ArtifactId: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Error: result.Error
                );
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid input for dump collection: {Message}", ex.Message);
            return new CollectDumpResult(
                Success: false,
                SessionId: string.Empty,
                ArtifactId: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Error: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collect dump failed for PID {Pid}", pid);
            return new CollectDumpResult(
                Success: false,
                SessionId: string.Empty,
                ArtifactId: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Error: $"Collect dump failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "collect.trace", Title = "Collect EventPipe Trace (Experimental)", ReadOnly = false, Idempotent = false), Description("Сбор EventPipe trace из .NET процесса через StartEventPipeSession(). EXPERIMENTAL: имеет workaround для PC2 (session.Stop() висит). Duration формат: dd:hh:mm")]
    public async Task<CollectTraceResult> CollectTrace(
        [Description("Process ID to collect trace from")] int pid,
        [Description("Duration in dd:hh:mm format (e.g., 00:01:30 for 1.5 minutes)")] string duration,
        [Description("Trace profile (optional)")] string? profile = null,
        CancellationToken cancellationToken = default)
    {
        if (pid <= 0)
        {
            throw new ArgumentException("Process ID must be greater than 0", nameof(pid));
        }

        if (string.IsNullOrWhiteSpace(duration))
        {
            throw new ArgumentException("Duration must not be empty", nameof(duration));
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(duration, @"^\d{2}:\d{2}:\d{2}$"))
        {
            throw new ArgumentException("Duration must be in dd:hh:mm format (e.g., 00:01:30)", nameof(duration));
        }

        try
        {
            _logger.LogInformation("Starting trace collection for PID {Pid}, Duration={Duration}, Profile={Profile}", pid, duration, profile);
            
            var request = new CollectTraceRequest(pid, duration, profile);
            var result = await _traceService.CollectTraceAsync(request, cancellationToken);
            
            if (result.Artifact != null)
            {
                _logger.LogInformation("Trace collected successfully: SessionId={SessionId}, ArtifactId={ArtifactId}", 
                    result.Session.SessionId.Value, result.Artifact.ArtifactId.Value);
                
                return new CollectTraceResult(
                    Success: true,
                    SessionId: result.Session.SessionId.Value,
                    ArtifactId: result.Artifact.ArtifactId.Value,
                    FilePath: result.Artifact.FilePath,
                    SizeBytes: result.Artifact.SizeBytes,
                    Sha256: result.Artifact.Sha256,
                    Error: null
                );
            }
            else
            {
                _logger.LogWarning("Trace collection failed for PID {Pid}: {Error}", pid, result.Error);
                return new CollectTraceResult(
                    Success: false,
                    SessionId: result.Session.SessionId.Value,
                    ArtifactId: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Error: result.Error
                );
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid input for trace collection: {Message}", ex.Message);
            return new CollectTraceResult(
                Success: false,
                SessionId: string.Empty,
                ArtifactId: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Error: $"Invalid input: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collect trace failed for PID {Pid}", pid);
            return new CollectTraceResult(
                Success: false,
                SessionId: string.Empty,
                ArtifactId: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Error: $"Collect trace failed: {ex.Message}"
            );
        }
    }
}

public record CollectDumpResult(
    bool Success,
    string SessionId,
    string? ArtifactId,
    string? FilePath,
    long SizeBytes,
    string? Sha256,
    string? Error
);

public record CollectTraceResult(
    bool Success,
    string SessionId,
    string? ArtifactId,
    string? FilePath,
    long SizeBytes,
    string? Sha256,
    string? Error
);
