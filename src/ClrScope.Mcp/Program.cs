using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services;
using ClrScope.Mcp.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;

namespace ClrScope.Mcp;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                var contentRoot = context.HostingEnvironment.ContentRootPath;
                config.SetBasePath(contentRoot);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<ClrScopeOptions>(
                    context.Configuration.GetSection(ClrScopeOptions.SectionName));

                // Infrastructure
                services.AddSingleton<SqliteSchemaInitializer>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<ClrScopeOptions>>();
                    var dbPath = options.Value.GetDatabasePath();
                    var directory = Path.GetDirectoryName(dbPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    var connectionString = $"Data Source={dbPath}";
                    return new SqliteSchemaInitializer(connectionString);
                });

                services.AddSingleton<ISqliteSessionStore>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<ClrScopeOptions>>();
                    var connectionString = $"Data Source={options.Value.GetDatabasePath()}";
                    return new SqliteSessionStore(connectionString);
                });

                services.AddSingleton<ISqliteArtifactStore>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<ClrScopeOptions>>();
                    var connectionString = $"Data Source={options.Value.GetDatabasePath()}";
                    return new SqliteArtifactStore(connectionString);
                });

                // Validation
                services.AddSingleton<IPreflightValidator, PreflightValidator>();

                // CLI Runner
                services.AddSingleton<ICliCommandRunner, CliCommandRunner>();

                // Services
                services.AddSingleton<HealthService>();
                services.AddSingleton<RuntimeService>();
                services.AddSingleton<InspectTargetService>();
                services.AddSingleton<CollectTraceService>();
                services.AddSingleton<CollectDumpService>();

                // MCP Server
                services.AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly();
            })
            .Build();

        // Check for demo mode
        if (args.Contains("--demo"))
        {
            await RunDemoAsync(host);
            return;
        }

        // Initialize database schema
        var schemaInitializer = host.Services.GetRequiredService<SqliteSchemaInitializer>();
        await schemaInitializer.InitializeAsync();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("CLRScope MCP Server - Stage 0c Integration");
        logger.LogInformation("Starting MCP server with stdio transport");

        await host.RunAsync();
    }

    static async Task RunDemoAsync(IHost host)
    {
        Console.WriteLine("=== CLRScope Vertical Slice Demo ===");
        Console.WriteLine();

        // Initialize database
        Console.WriteLine("[1] Initializing database...");
        var schemaInitializer = host.Services.GetRequiredService<SqliteSchemaInitializer>();
        await schemaInitializer.InitializeAsync();
        Console.WriteLine("✓ Database initialized");
        Console.WriteLine();

        // Health check
        Console.WriteLine("[2] Health check...");
        var healthService = host.Services.GetRequiredService<HealthService>();
        var health = await healthService.GetHealthAsync();
        Console.WriteLine($"✓ IsHealthy: {health.IsHealthy}");
        Console.WriteLine($"  Version: {health.Version}");
        Console.WriteLine($"  ArtifactRoot: {health.ArtifactRoot.Path}");
        Console.WriteLine($"  Database: {health.Database.Path}");
        if (health.Warnings.Length > 0)
        {
            Console.WriteLine("  Warnings:");
            foreach (var warning in health.Warnings)
            {
                Console.WriteLine($"    - {warning}");
            }
        }
        Console.WriteLine();

        // List targets
        Console.WriteLine("[3] Listing .NET targets...");
        var runtimeService = host.Services.GetRequiredService<RuntimeService>();
        var targets = runtimeService.ListTargets();
        Console.WriteLine($"✓ Found {targets.Count} .NET processes:");
        foreach (var target in targets)
        {
            Console.WriteLine($"  - PID {target.Pid}: {target.ProcessName}");
        }
        Console.WriteLine();

        if (targets.Count == 0)
        {
            Console.WriteLine("No .NET processes found. Demo requires at least one .NET process.");
            return;
        }

        // Inspect first target
        var firstTarget = targets[0];
        Console.WriteLine($"[4] Inspecting target PID {firstTarget.Pid}...");
        var inspectService = host.Services.GetRequiredService<InspectTargetService>();
        var inspectResult = inspectService.InspectTarget(firstTarget.Pid);
        Console.WriteLine($"✓ Found: {inspectResult.Found}");
        Console.WriteLine($"  Attachable: {inspectResult.Attachable}");
        if (inspectResult.Details != null)
        {
            Console.WriteLine($"  ProcessName: {inspectResult.Details.ProcessName}");
            Console.WriteLine($"  CommandLine: {inspectResult.Details.CommandLine ?? "null"}");
            Console.WriteLine($"  OS: {inspectResult.Details.OperatingSystem}");
            Console.WriteLine($"  Architecture: {inspectResult.Details.ProcessArchitecture}");
        }
        if (inspectResult.Warnings.Length > 0)
        {
            Console.WriteLine("  Warnings:");
            foreach (var warning in inspectResult.Warnings)
            {
                Console.WriteLine($"    - {warning}");
            }
        }
        Console.WriteLine();

        // Collect dump
        Console.WriteLine($"[5] Collecting dump from PID {firstTarget.Pid}...");
        var dumpService = host.Services.GetRequiredService<CollectDumpService>();
        var dumpRequest = new CollectDumpRequest(firstTarget.Pid, IncludeHeap: true);
        var dumpResult = await dumpService.CollectDumpAsync(dumpRequest);
        if (dumpResult.Artifact != null)
        {
            Console.WriteLine("✓ Dump collected successfully");
            Console.WriteLine($"  Session ID: {dumpResult.Session.SessionId.Value}");
            Console.WriteLine($"  Artifact ID: {dumpResult.Artifact.ArtifactId.Value}");
            Console.WriteLine($"  File Path: {dumpResult.Artifact.FilePath}");
            Console.WriteLine($"  Size: {dumpResult.Artifact.SizeBytes} bytes");
            Console.WriteLine($"  SHA256: {dumpResult.Artifact.Sha256}");
        }
        else
        {
            Console.WriteLine($"✗ Dump collection failed: {dumpResult.Error}");
        }
        Console.WriteLine();

        Console.WriteLine("=== Demo Complete ===");
    }
}
