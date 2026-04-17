using ClrScope.Mcp.Infrastructure;
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
    public static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand
        {
            Description = "CLRScope MCP - .NET diagnostics MCP server"
        };

        // Run command (default)
        var runCommand = new Command("run", "Run the MCP server")
        {
            IsHidden = true // Hidden since it's the default
        };
        runCommand.SetHandler(async () =>
        {
            var host = Bootstrap.BuildHost();
            await RunServerAsync(host);
        });
        rootCommand.AddCommand(runCommand);

        // Demo command
        var demoCommand = new Command("demo", "Run demo mode");
        demoCommand.SetHandler(async () =>
        {
            var host = Bootstrap.BuildHost();
            await RunDemoAsync(host);
        });
        rootCommand.AddCommand(demoCommand);

        // Version option
        var versionOption = new Option<bool>(new[] { "--version", "-v" }, "Show version information");
        rootCommand.AddOption(versionOption);

        // Help option
        var helpOption = new Option<bool>(new[] { "--help", "-h" }, "Show help information");
        rootCommand.AddOption(helpOption);

        // Set default handler (run server)
        rootCommand.SetHandler(async () =>
        {
            var host = Bootstrap.BuildHost();
            await RunServerAsync(host);
        });

        return rootCommand;
    }

    /// <summary>
    /// Parse and invoke the command line
    /// </summary>
    public static async Task<int> ParseAndInvokeAsync(string[] args)
    {
        var rootCommand = BuildRootCommand();

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

        Console.WriteLine("=== Demo Complete ===");
    }

    private static void PrintVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        Console.WriteLine($"CLRScope MCP v{version}");
    }
}
