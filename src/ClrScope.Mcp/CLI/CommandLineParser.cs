using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Services.Collect;
using ClrScope.Mcp.Services.Health;
using ClrScope.Mcp.Services.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace ClrScope.Mcp.CLI;

/// <summary>
/// CLI parser using System.CommandLine
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Build and return the root command
    /// </summary>
    public static RootCommand BuildRootCommand(string[] args)
    {
        var rootCommand = new RootCommand
        {
            Description = "CLRScope MCP - .NET diagnostics MCP server"
        };

        // Run command (default)
        var runCommand = new Command("run", "Run the MCP server");
        runCommand.SetHandler(async () =>
        {
            var host = Bootstrap.BuildHost(args);
            await RunServerAsync(host);
        });
        rootCommand.AddCommand(runCommand);

        // Demo command
        var demoCommand = new Command("demo", "Run demo mode")
        {
            IsHidden = true
        };
        demoCommand.SetHandler(async () =>
        {
            var host = Bootstrap.BuildHost(args);
            await RunDemoAsync(host);
        });
        rootCommand.AddCommand(demoCommand);

        // Set default handler (run server)
        rootCommand.SetHandler(async () =>
        {
            var host = Bootstrap.BuildHost(args);
            await RunServerAsync(host);
        });

        return rootCommand;
    }

    /// <summary>
    /// Parse and invoke the command line
    /// </summary>
    public static async Task<int> ParseAndInvokeAsync(string[] args)
    {
        var rootCommand = BuildRootCommand(args);

        // Handle --version and -v
        if (args.Contains("--version") || args.Contains("-v"))
        {
            PrintVersion();
            return 0;
        }

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunServerAsync(IHost host)
    {
        var schemaInitializer = host.Services.GetRequiredService<SqliteSchemaInitializer>();
        await schemaInitializer.InitializeAsync();
        await host.RunAsync();
    }

    private static async Task RunDemoAsync(IHost host)
    {
        Console.WriteLine("=== CLRScope Vertical Slice Demo ===");
        Console.WriteLine();

        var schemaInitializer = host.Services.GetRequiredService<SqliteSchemaInitializer>();
        await schemaInitializer.InitializeAsync();
        Console.WriteLine("✓ Database initialized");
        Console.WriteLine();

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

        var firstTarget = targets[0];
        Console.WriteLine($"✓ Inspecting target PID {firstTarget.Pid}...");
        var inspectService = host.Services.GetRequiredService<InspectTargetService>();
        var inspectResult = inspectService.InspectTarget(firstTarget.Pid);
        Console.WriteLine($"  Attachable: {inspectResult.Attachable}");
        if (inspectResult.Details != null)
        {
            Console.WriteLine($"  ProcessName: {inspectResult.Details.ProcessName}");
            Console.WriteLine($"  CommandLine: {inspectResult.Details.CommandLine ?? "null"}");
            Console.WriteLine($"  OS: {inspectResult.Details.OperatingSystem}");
            Console.WriteLine($"  Architecture: {inspectResult.Details.ProcessArchitecture}");
        }
        Console.WriteLine();

        // Collect dump
        Console.WriteLine($"✓ Collecting dump from PID {firstTarget.Pid}...");
        var dumpService = host.Services.GetRequiredService<CollectDumpService>();
        var dumpRequest = new CollectDumpRequest(firstTarget.Pid, IncludeHeap: true);
        var dumpResult = await dumpService.CollectDumpAsync(dumpRequest);
        if (dumpResult.Artifact != null)
        {
            Console.WriteLine("  Session ID: {0}", dumpResult.Session.SessionId.Value);
            Console.WriteLine("  Artifact ID: {0}", dumpResult.Artifact.ArtifactId.Value);
            Console.WriteLine("  File Path: {0}", dumpResult.Artifact.FilePath);
            Console.WriteLine("  Size: {0} bytes", dumpResult.Artifact.SizeBytes);
            Console.WriteLine("  SHA256: {0} ({1})", dumpResult.Artifact.Sha256 ?? "N/A", dumpResult.Artifact.HashState);
        }
        else
        {
            Console.WriteLine("  Dump collection failed: {0}", dumpResult.Error);
        }
        Console.WriteLine();

        Console.WriteLine("=== Demo Complete ===");
    }

    private static void PrintVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        Console.WriteLine($"CLRScope MCP v{version}");
    }
}
