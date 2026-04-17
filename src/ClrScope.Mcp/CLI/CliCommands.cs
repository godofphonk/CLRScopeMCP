using ClrScope.Mcp.DependencyInjection;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Health;
using ClrScope.Mcp.Services.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.CLI;

public enum CliCommand
{
    Run,
    Help,
    Version,
    Demo
}

public static class CliCommands
{
    public static CliCommand ParseCommand(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
            return CliCommand.Help;
        
        if (args.Contains("--version") || args.Contains("-v"))
            return CliCommand.Version;
        
        if (args.Contains("--demo"))
            return CliCommand.Demo;
        
        return CliCommand.Run;
    }

    public static IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var contentRoot = context.HostingEnvironment.ContentRootPath;
                config.SetBasePath(contentRoot);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<ClrScopeOptions>(
                    context.Configuration.GetSection(ClrScopeOptions.SectionName));
                services.AddClrScopeStorage();
                services.AddClrScopeDiagnostics();
                services.AddClrScopeCollectionServices();
                services.AddClrScopeWorkflows();
                services.AddClrScopeMcpTools();
            })
            .Build();
    }

    public static async Task<int> ExecuteAsync(CliCommand command, string[] args)
    {
        switch (command)
        {
            case CliCommand.Help:
                PrintHelp();
                return 0;
            
            case CliCommand.Version:
                PrintVersion();
                return 0;
            
            case CliCommand.Demo:
                var demoHost = BuildHost();
                await RunDemoAsync(demoHost);
                return 0;
            
            case CliCommand.Run:
                var host = BuildHost();
                await RunServerAsync(host);
                return 0;
            
            default:
                PrintHelp();
                return 1;
        }
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

    private static void PrintHelp()
    {
        Console.WriteLine("CLRScope MCP - .NET diagnostics MCP server");
        Console.WriteLine();
        Console.WriteLine("Usage: clrscope-mcp [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --version, -v    Show version information");
        Console.WriteLine("  --help, -h      Show help information");
        Console.WriteLine("  --demo          Run demo mode");
        Console.WriteLine();
        Console.WriteLine("CLRScope MCP provides AI-powered diagnostic capabilities for .NET");
        Console.WriteLine("applications through the Model Context Protocol.");
    }
}
