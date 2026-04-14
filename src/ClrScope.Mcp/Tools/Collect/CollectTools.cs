using ClrScope.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Collect;

[McpServerToolType]
public sealed class CollectTools
{
    [McpServerTool(Name = "collect_dump", Title = "Collect Memory Dump", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false), Description("Collect memory dump from .NET process via WriteDump(). Returns Session ID and Artifact ID. For long-running dumps (can take minutes), use session_get with the Session ID to track progress via Phase and Status. CANCELLATION SEMANTICS: best-effort only - session.cancel updates session state but does NOT guarantee termination of native dump generation. Once WriteDump starts, it runs to completion regardless of cancellation due to DiagnosticsClient API limitations.")]
    public static async Task<CollectDumpResult> CollectDump(
        [Description("Process ID to collect dump from")] int pid,
        McpServer server,
        [Description("Include heap in dump (default: true)")] bool includeHeap = true,
        [Description("Compress dump file with gzip to save disk space (default: false)")] bool compress = false,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var dumpService = server.Services!.GetRequiredService<CollectDumpService>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectTools>>();

        try
        {
            if (pid <= 0)
            {
                return new CollectDumpResult(
                    Success: false,
                    SessionId: string.Empty,
                    ArtifactId: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Error: "Process ID must be greater than 0",
                    CancellationSemantics: "best_effort"
                );
            }

            logger.LogInformation("Starting dump collection for PID {Pid}, IncludeHeap={IncludeHeap}, Compress={Compress}", pid, includeHeap, compress);

            var request = new CollectDumpRequest(pid, includeHeap, compress);
            var result = await dumpService.CollectDumpAsync(request, progress, cancellationToken);
            
            if (result.Artifact != null)
            {
                logger.LogInformation("Dump collected successfully: SessionId={SessionId}, ArtifactId={ArtifactId}", 
                    result.Session.SessionId.Value, result.Artifact.ArtifactId.Value);
                
                return new CollectDumpResult(
                    Success: true,
                    SessionId: result.Session.SessionId.Value,
                    ArtifactId: result.Artifact.ArtifactId.Value,
                    FilePath: result.Artifact.FilePath,
                    SizeBytes: result.Artifact.SizeBytes,
                    Sha256: result.Artifact.Sha256,
                    HashState: result.Artifact.HashState.ToString(),
                    Error: null,
                    CancellationSemantics: "best_effort"
                );
            }
            else
            {
                logger.LogWarning("Dump collection failed for PID {Pid}: {Error}", pid, result.Error);
                return new CollectDumpResult(
                    Success: false,
                    SessionId: result.Session.SessionId.Value,
                    ArtifactId: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Error: result.Error,
                    CancellationSemantics: "best_effort"
                );
            }
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for dump collection: {Message}", ex.Message);
            return new CollectDumpResult(
                Success: false,
                SessionId: string.Empty,
                ArtifactId: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Error: $"Invalid input: {ex.Message}",
                CancellationSemantics: "best_effort"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collect dump failed for PID {Pid}", pid);
            return new CollectDumpResult(
                Success: false,
                SessionId: string.Empty,
                ArtifactId: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Error: $"Collect dump failed: {ex.Message}",
                CancellationSemantics: "best_effort"
            );
        }
    }

    [McpServerTool(Name = "collect_trace", Title = "Collect EventPipe Trace (Experimental)", ReadOnly = false, Idempotent = false), Description("Collect EventPipe trace from .NET process via StartEventPipeSession(). Duration format: hh:mm:ss. Profile: cpu-sampling, gc-heap, or default. Custom providers format: 'ProviderName:Level:Keywords' (e.g., 'Microsoft-Windows-DotNETRuntime:Informational:0x00000001'). For long-running traces, use session_get with the Session ID to track progress via Phase and Status.")]
    public static async Task<CollectTraceResult> CollectTrace(
        [Description("Process ID to collect trace from")] int pid,
        [Description("Duration in hh:mm:ss format (e.g., 00:01:30 for 1.5 minutes)")] string duration,
        McpServer server,
        IProgress<double>? progress = null,
        [Description("Trace profile: cpu-sampling, gc-heap, or default")] string? profile = null,
        [Description("Custom providers in format 'ProviderName:Level:Keywords' (e.g., 'Microsoft-Windows-DotNETRuntime:Informational:0x00000001'). Overrides profile if specified.")] string[]? customProviders = null,
        CancellationToken cancellationToken = default)
    {
        var traceService = server.Services!.GetRequiredService<CollectTraceService>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectTools>>();

        try
        {
            if (pid <= 0)
            {
                return new CollectTraceResult(
                    Success: false,
                    SessionId: string.Empty,
                    ArtifactId: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Error: "Process ID must be greater than 0",
                    CompletionMode: "Failed"
                );
            }

            if (string.IsNullOrWhiteSpace(duration))
            {
                return new CollectTraceResult(
                    Success: false,
                    SessionId: string.Empty,
                    ArtifactId: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Error: "Duration must not be empty",
                    CompletionMode: "Failed"
                );
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(duration, @"^\d{2}:\d{2}:\d{2}$"))
            {
                return new CollectTraceResult(
                    Success: false,
                    SessionId: string.Empty,
                    ArtifactId: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Error: "Duration must be in hh:mm:ss format (e.g., 00:01:30)",
                    CompletionMode: "Failed"
                );
            }

            logger.LogInformation("Starting trace collection for PID {Pid}, Duration={Duration}, Profile={Profile}, CustomProviders={CustomProviders}", pid, duration, profile ?? "default", customProviders != null ? string.Join(", ", customProviders) : "none");

            var request = new CollectTraceRequest(pid, duration, profile, customProviders);
            var result = await traceService.CollectTraceAsync(request, progress, cancellationToken);
            
            if (result.Artifact != null)
            {
                logger.LogInformation("Trace collected successfully: SessionId={SessionId}, ArtifactId={ArtifactId}, CompletionMode={CompletionMode}",
                    result.Session.SessionId.Value, result.Artifact.ArtifactId.Value, result.CompletionMode);

                return new CollectTraceResult(
                    Success: true,
                    SessionId: result.Session.SessionId.Value,
                    ArtifactId: result.Artifact.ArtifactId.Value,
                    FilePath: result.Artifact.FilePath,
                    SizeBytes: result.Artifact.SizeBytes,
                    Sha256: result.Artifact.Sha256,
                    HashState: result.Artifact.HashState.ToString(),
                    Error: null,
                    CompletionMode: result.CompletionMode.ToString()
                );
            }
            else
            {
                logger.LogWarning("Trace collection failed for PID {Pid}: {Error}, CompletionMode={CompletionMode}", pid, result.Error, result.CompletionMode);
                return new CollectTraceResult(
                    Success: false,
                    SessionId: result.Session.SessionId.Value,
                    ArtifactId: null,
                    FilePath: null,
                    SizeBytes: 0,
                    Sha256: null,
                    Error: result.Error,
                    CompletionMode: result.CompletionMode.ToString()
                );
            }
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for trace collection: {Message}", ex.Message);
            return new CollectTraceResult(
                Success: false,
                SessionId: string.Empty,
                ArtifactId: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Error: $"Invalid input: {ex.Message}",
                CompletionMode: "Failed"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collect trace failed for PID {Pid}", pid);
            return new CollectTraceResult(
                Success: false,
                SessionId: string.Empty,
                ArtifactId: null,
                FilePath: null,
                SizeBytes: 0,
                Sha256: null,
                Error: $"Collect trace failed: {ex.Message}",
                CompletionMode: "Failed"
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
    string? HashState,
    string? Error,
    string CancellationSemantics = "best_effort"
);

public record CollectTraceResult(
    bool Success,
    string SessionId,
    string? ArtifactId,
    string? FilePath,
    long SizeBytes,
    string? Sha256,
    string? HashState,
    string? Error,
    string? CompletionMode = "Complete"
);
