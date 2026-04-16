using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;

namespace ClrScope.Mcp.Tools.Collect;

[McpServerToolType]
public sealed class CollectCountersTools
{
    [McpServerTool(Name = "collect_counters", Title = "Collect Performance Counters", ReadOnly = false, Idempotent = false), Description("Collect performance counters via dotnet-counters CLI. Available providers: System.Runtime, Microsoft.AspNetCore.Hosting, System.Net.Http, System.Net.NameResolution, System.Net.Security, System.Net.Sockets, Microsoft.AspNetCore.Kestrel, Microsoft.AspNetCore.Routing, Microsoft.AspNetCore.RateLimiting. For long-running operations, use session_get with the Session ID to track progress via Phase and Status.")]
    public static async Task<CollectCountersResult> CollectCounters(
        [Description("Process ID to collect counters from")] int pid,
        McpServer server,
        [Description("Duration in hh:mm:ss format (e.g., 00:01:00 for 1 minute)")] string duration = "00:01:00",
        [Description("Counter providers (e.g., System.Runtime, Microsoft.AspNetCore.Hosting). Defaults to System.Runtime if not specified.")] string[]? providers = null,
        CancellationToken cancellationToken = default)
    {
        // Validate PID before getting services
        if (pid <= 0)
        {
            return new CollectCountersResult(
                Success: false,
                Message: "Process ID must be greater than 0",
                ArtifactId: null,
                SessionId: null
            );
        }

        // Validate duration before getting services
        if (string.IsNullOrWhiteSpace(duration))
        {
            return new CollectCountersResult(
                Success: false,
                Message: "Duration must not be empty",
                ArtifactId: null,
                SessionId: null
            );
        }

        if (!TimeSpan.TryParseExact(duration, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out var _))
        {
            return new CollectCountersResult(
                Success: false,
                Message: "Duration must be in hh:mm:ss format (e.g., 00:01:00)",
                ArtifactId: null,
                SessionId: null
            );
        }

        var countersService = server.Services!.GetRequiredService<CollectCountersService>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectCountersTools>>();

        try
        {
            // Validate and set default provider
            var providerList = providers != null && providers.Length > 0 ? providers : new[] { "System.Runtime" };

            // Validate provider names (basic validation)
            var knownProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System.Runtime",
                "Microsoft.AspNetCore.Hosting",
                "System.Net.Http",
                "System.Net.NameResolution",
                "System.Net.Security",
                "System.Net.Sockets",
                "Microsoft.AspNetCore.Kestrel",
                "Microsoft.AspNetCore.Routing",
                "Microsoft.AspNetCore.RateLimiting",
                "Microsoft.AspNetCore.Http.Connections",
                "Microsoft.AspNetCore.Server.Kestrel"
            };

            var invalidProviders = providerList.Where(p => !knownProviders.Contains(p)).ToList();
            if (invalidProviders.Any())
            {
                logger.LogWarning("Unknown providers: {Providers}", string.Join(", ", invalidProviders));
            }

            var request = new CollectCountersRequest(pid, duration, providerList);
            var result = await countersService.CollectCountersAsync(request, cancellationToken: cancellationToken);

            if (result.Artifact != null)
            {
                return new CollectCountersResult(
                    Success: true,
                    Message: $"Counters collected successfully: {result.Artifact.ArtifactId.Value}",
                    ArtifactId: result.Artifact.ArtifactId.Value,
                    SessionId: result.Session.SessionId.Value
                );
            }
            else
            {
                return new CollectCountersResult(
                    Success: false,
                    Message: result.Error ?? "Counter collection failed",
                    ArtifactId: null,
                    SessionId: result.Session.SessionId.Value
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collect counters failed for PID {Pid}", pid);
            return new CollectCountersResult(
                Success: false,
                Message: $"Collect counters failed: {ex.Message}",
                ArtifactId: null,
                SessionId: null
            );
        }
    }

    [McpServerTool(Name = "collect_gcdump", Title = "Collect GC Heap Snapshot", ReadOnly = false, Idempotent = false), Description("Collect GC heap snapshot via dotnet-gcdump CLI. For long-running operations, use session_get with the Session ID to track progress via Phase and Status.")]
    public static async Task<CollectGcDumpResult> CollectGcDump(
        [Description("Process ID to collect GC heap snapshot from")] int pid,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        // Validate PID before getting services
        if (pid <= 0)
        {
            return new CollectGcDumpResult(
                Success: false,
                Message: "Process ID must be greater than 0",
                ArtifactId: null,
                SessionId: null
            );
        }

        var gcdumpService = server.Services!.GetRequiredService<CollectGcDumpService>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectCountersTools>>();

        try
        {
            var request = new ClrScope.Mcp.Services.CollectGcDumpRequest(pid);
            var result = await gcdumpService.CollectGcDumpAsync(request, cancellationToken: cancellationToken);

            if (result.Artifact != null)
            {
                return new CollectGcDumpResult(
                    Success: true,
                    Message: $"GC heap snapshot collected successfully: {result.Artifact.ArtifactId.Value}",
                    ArtifactId: result.Artifact.ArtifactId.Value,
                    SessionId: result.Session.SessionId.Value
                );
            }
            else
            {
                return new CollectGcDumpResult(
                    Success: false,
                    Message: result.Error ?? "GC heap snapshot collection failed",
                    ArtifactId: null,
                    SessionId: result.Session.SessionId.Value
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collect GC heap snapshot failed for PID {Pid}", pid);
            return new CollectGcDumpResult(
                Success: false,
                Message: $"Collect GC heap snapshot failed: {ex.Message}",
                ArtifactId: null,
                SessionId: null
            );
        }
    }

    [McpServerTool(Name = "import_gcdump", Title = "Import GC Heap Snapshot File", ReadOnly = false, Idempotent = false), Description("Import existing .gcdump file as artifact for analysis")]
    public static async Task<ImportArtifactResult> ImportGcDump(
        [Description("Path to .gcdump file to import")] string filePath,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var sessionStore = server.Services!.GetRequiredService<ISqliteSessionStore>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectCountersTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new ImportArtifactResult(
                    Success: false,
                    ArtifactId: null,
                    Error: "File path is required"
                );
            }

            if (!File.Exists(filePath))
            {
                return new ImportArtifactResult(
                    Success: false,
                    ArtifactId: null,
                    Error: $"File not found: {filePath}"
                );
            }

            if (!filePath.EndsWith(".gcdump", StringComparison.OrdinalIgnoreCase))
            {
                return new ImportArtifactResult(
                    Success: false,
                    ArtifactId: null,
                    Error: "File must have .gcdump extension"
                );
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                return new ImportArtifactResult(
                    Success: false,
                    ArtifactId: null,
                    Error: "File is empty"
                );
            }

            var artifactRoot = options.Value.GetArtifactRoot();
            var gcdumpsDir = Path.Combine(artifactRoot, "gcdumps");
            Directory.CreateDirectory(gcdumpsDir);

            // Create session for import
            var session = await sessionStore.CreateAsync(SessionKind.GcDump, 0, "import", cancellationToken);

            var artifactId = ArtifactId.New();
            var fileName = $"gcdump_{artifactId.Value}.gcdump";
            var destPath = Path.Combine(gcdumpsDir, fileName);

            File.Copy(filePath, destPath, overwrite: true);
            logger.LogInformation("Imported .gcdump file from {SourcePath} to {DestPath}", filePath, destPath);

            var artifact = await artifactStore.CreateAsync(
                ArtifactKind.GcDump,
                destPath,
                fileInfo.Length,
                0, // No PID for imported files
                session.SessionId,
                cancellationToken
            );

            var diagUri = $"clrscope://artifact/{artifact.ArtifactId.Value}";
            var fileUri = new Uri(destPath).AbsoluteUri;
            artifact = artifact with { DiagUri = diagUri, FileUri = fileUri, Status = ArtifactStatus.Completed };
            await artifactStore.UpdateAsync(artifact, cancellationToken);

            return new ImportArtifactResult(
                Success: true,
                ArtifactId: artifact.ArtifactId.Value,
                Error: null
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import .gcdump file failed for {FilePath}", filePath);
            return new ImportArtifactResult(
                Success: false,
                ArtifactId: null,
                Error: $"Import failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "import_trace", Title = "Import EventPipe Trace File", ReadOnly = false, Idempotent = false), Description("Import existing .nettrace file as artifact for analysis")]
    public static async Task<ImportArtifactResult> ImportTrace(
        [Description("Path to .nettrace file to import")] string filePath,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var sessionStore = server.Services!.GetRequiredService<ISqliteSessionStore>();
        var options = server.Services!.GetRequiredService<IOptions<ClrScopeOptions>>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectCountersTools>>();

        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new ImportArtifactResult(
                    Success: false,
                    ArtifactId: null,
                    Error: "File path is required"
                );
            }

            if (!File.Exists(filePath))
            {
                return new ImportArtifactResult(
                    Success: false,
                    ArtifactId: null,
                    Error: $"File not found: {filePath}"
                );
            }

            if (!filePath.EndsWith(".nettrace", StringComparison.OrdinalIgnoreCase))
            {
                return new ImportArtifactResult(
                    Success: false,
                    ArtifactId: null,
                    Error: "File must have .nettrace extension"
                );
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                return new ImportArtifactResult(
                    Success: false,
                    ArtifactId: null,
                    Error: "File is empty"
                );
            }

            var artifactRoot = options.Value.GetArtifactRoot();
            var tracesDir = Path.Combine(artifactRoot, "traces");
            Directory.CreateDirectory(tracesDir);

            // Create session for import
            var session = await sessionStore.CreateAsync(SessionKind.Trace, 0, "import", cancellationToken);

            var artifactId = ArtifactId.New();
            var fileName = $"trace_{artifactId.Value}.nettrace";
            var destPath = Path.Combine(tracesDir, fileName);

            File.Copy(filePath, destPath, overwrite: true);
            logger.LogInformation("Imported .nettrace file from {SourcePath} to {DestPath}", filePath, destPath);

            var artifact = await artifactStore.CreateAsync(
                ArtifactKind.Trace,
                destPath,
                fileInfo.Length,
                0, // No PID for imported files
                session.SessionId,
                cancellationToken
            );

            var diagUri = $"clrscope://artifact/{artifact.ArtifactId.Value}";
            var fileUri = new Uri(destPath).AbsoluteUri;
            artifact = artifact with { DiagUri = diagUri, FileUri = fileUri, Status = ArtifactStatus.Completed };
            await artifactStore.UpdateAsync(artifact, cancellationToken);

            return new ImportArtifactResult(
                Success: true,
                ArtifactId: artifact.ArtifactId.Value,
                Error: null
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import .nettrace file failed for {FilePath}", filePath);
            return new ImportArtifactResult(
                Success: false,
                ArtifactId: null,
                Error: $"Import failed: {ex.Message}"
            );
        }
    }

    [McpServerTool(Name = "collect_stacks", Title = "Collect Managed Stacks", ReadOnly = false, Idempotent = false), Description("Collect managed stacks via dotnet-stack CLI. Output format: text (plain text) or json (structured JSON for parsing). For long-running operations, use session_get with the Session ID to track progress via Phase and Status.")]
    public static async Task<CollectStacksResult> CollectStacks(
        [Description("Process ID to collect managed stacks from")] int pid,
        McpServer server,
        [Description("Output format: 'text' (plain text) or 'json' (structured JSON)")] string format = "text",
        CancellationToken cancellationToken = default)
    {
        // Validate PID before getting services
        if (pid <= 0)
        {
            return new CollectStacksResult(
                Success: false,
                Message: "Process ID must be greater than 0",
                ArtifactId: null,
                SessionId: null
            );
        }

        // Validate format before getting services
        if (format != "text" && format != "json")
        {
            return new CollectStacksResult(
                Success: false,
                Message: "Format must be 'text' or 'json'",
                ArtifactId: null,
                SessionId: null
            );
        }

        var stacksService = server.Services!.GetRequiredService<CollectStacksService>();
        var logger = server.Services!.GetRequiredService<ILogger<CollectCountersTools>>();

        try
        {
            var request = new ClrScope.Mcp.Services.CollectStacksRequest(pid, format);
            var result = await stacksService.CollectStacksAsync(request, cancellationToken: cancellationToken);

            if (result.Artifact != null)
            {
                return new CollectStacksResult(
                    Success: true,
                    Message: $"Managed stacks collected successfully: {result.Artifact.ArtifactId.Value}",
                    ArtifactId: result.Artifact.ArtifactId.Value,
                    SessionId: result.Session.SessionId.Value
                );
            }
            else
            {
                return new CollectStacksResult(
                    Success: false,
                    Message: result.Error ?? "Managed stacks collection failed",
                    ArtifactId: null,
                    SessionId: result.Session.SessionId.Value
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collect managed stacks failed for PID {Pid}", pid);
            return new CollectStacksResult(
                Success: false,
                Message: $"Collect managed stacks failed: {ex.Message}",
                ArtifactId: null,
                SessionId: null
            );
        }
    }
}

public record CollectCountersResult(
    bool Success,
    string Message,
    string? ArtifactId,
    string? SessionId = null
);

public record CollectGcDumpResult(
    bool Success,
    string Message,
    string? ArtifactId,
    string? SessionId = null
);

public record CollectStacksResult(
    bool Success,
    string Message,
    string? ArtifactId,
    string? SessionId = null
);

public record ImportArtifactResult(
    bool Success,
    string? ArtifactId,
    string? Error
);
