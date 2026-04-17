using ClrScope.Mcp.DependencyInjection;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.CLI;

/// <summary>
/// Bootstrap and DI configuration for the application
/// </summary>
public static class Bootstrap
{
    /// <summary>
    /// Build the application host with all services configured
    /// </summary>
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
}
