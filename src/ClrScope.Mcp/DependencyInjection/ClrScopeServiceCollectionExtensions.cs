using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Analysis.PatternDetectors;
using ClrScope.Mcp.Services.Collect;
using ClrScope.Mcp.Services.Health;
using ClrScope.Mcp.Services.Heap;
using ClrScope.Mcp.Services.Runtime;
using ClrScope.Mcp.Services.Workflows;
using ClrScope.Mcp.Tools.Analysis;
using ClrScope.Mcp.Tools.Artifacts;
using ClrScope.Mcp.Tools.Collect;
using ClrScope.Mcp.Tools.Resources;
using ClrScope.Mcp.Tools.Runtime;
using ClrScope.Mcp.Tools.Sessions;
using ClrScope.Mcp.Tools.SystemHealth;
using ClrScope.Mcp.Tools.Workflows;
using ClrScope.Mcp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol;

namespace ClrScope.Mcp.DependencyInjection;

public static class ClrScopeServiceCollectionExtensions
{
    /// <summary>
    /// Adds CLRScope storage services (SQLite stores and schema initializer)
    /// </summary>
    public static IServiceCollection AddClrScopeStorage(this IServiceCollection services)
    {
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

        return services;
    }

    /// <summary>
    /// Adds CLRScope diagnostics services (CLI runner, tool checker, symbol resolver, SOS analyzer)
    /// </summary>
    public static IServiceCollection AddClrScopeDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<ICliCommandRunner, CliCommandRunner>();
        services.AddSingleton<ICliToolAvailabilityChecker, CliToolAvailabilityChecker>();
        services.AddSingleton<ISymbolResolver, SymbolResolver>();
        services.AddSingleton<ISosAnalyzer, DotnetDumpAnalyzer>();
        services.AddSingleton<IHeapSnapshotPreparer, HeapSnapshotPreparer>();
        services.AddSingleton<IHeapSnapshotCache, HeapSnapshotCache>();
        services.AddSingleton<IGcDumpGraphAdapter, GcDumpProcessAdapter>();
        services.AddSingleton<DominatorTreeCalculator>();
        services.AddSingleton<HeapRetainerPathsBuilder>();

        // Pattern detectors
        services.AddSingleton<IPatternDetector, DumpPatternDetector>();
        services.AddSingleton<IPatternDetector, GcDumpPatternDetector>();
        services.AddSingleton<IPatternDetector, TracePatternDetector>();
        services.AddSingleton<IPatternDetector, CountersPatternDetector>();
        services.AddSingleton<IPatternDetector, StacksPatternDetector>();

        // Artifact content analyzer
        services.AddSingleton<ArtifactContentAnalyzer>();

        return services;
    }

    /// <summary>
    /// Adds CLRScope collection services (validators, lock manager, artifact retention, collection services)
    /// </summary>
    public static IServiceCollection AddClrScopeCollectionServices(this IServiceCollection services)
    {
        services.AddSingleton<IPreflightValidator, FullPreflightValidator>();
        services.AddSingleton<IPidLockManager, PidLockManager>();
        services.AddSingleton<IActiveOperationRegistry, ActiveOperationRegistry>();
        services.AddSingleton<IArtifactRetentionService, ArtifactRetentionService>();
        services.AddSingleton<CorrelationIdProvider>();
        services.AddSingleton<ICountersBackend, CliCountersBackend>();

        services.AddSingleton<HealthService>();
        services.AddSingleton<RuntimeService>();
        services.AddSingleton<InspectTargetService>();
        services.AddSingleton<CollectTraceService>();
        services.AddSingleton<CollectDumpService>();
        services.AddSingleton<CollectCountersService>();
        services.AddSingleton<CollectGcDumpService>();
        services.AddSingleton<CollectStacksService>();

        return services;
    }

    /// <summary>
    /// Adds CLRScope workflow services
    /// </summary>
    public static IServiceCollection AddClrScopeWorkflows(this IServiceCollection services)
    {
        services.AddSingleton<WorkflowOrchestrator>();
        services.AddSingleton<HighCpuWorkflow>();
        services.AddSingleton<MemoryLeakWorkflow>();
        services.AddSingleton<HangWorkflow>();
        services.AddSingleton<BaselineWorkflow>();

        return services;
    }

    /// <summary>
    /// Adds CLRScope MCP tools to the server
    /// </summary>
    public static IServiceCollection AddClrScopeMcpTools(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<RuntimeTools>()
            .WithTools<CollectTools>()
            .WithTools<CollectCountersTools>()
            .WithTools<SystemTools>()
            .WithTools<SessionTools>()
            .WithTools<ArtifactCrudTools>()
            .WithTools<ArtifactLifecycleTools>()
            .WithTools<AnalysisTools>()
            .WithTools<ResourceTools>()
            .WithTools<SummaryTools>()
            .WithTools<PatternDetectionTools>()
            .WithTools<HeapAnalysisTools>()
            .WithTools<SessionAnalysisTools>()
            .WithTools<WorkflowAutomationTools>();

        return services;
    }
}
