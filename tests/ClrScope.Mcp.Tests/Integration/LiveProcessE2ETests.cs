using ClrScope.Mcp.DependencyInjection;
using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Heap.Enums;
using ClrScope.Mcp.Domain.Heap.Options;
using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Collect;
using ClrScope.Mcp.Services.Heap;
using ClrScope.Mcp.Services.Runtime;
using ClrScope.Mcp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ClrScope.Mcp.Tests.Integration;

[Trait("Category", "Live")]
public class LiveProcessE2ETests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testProjectPath;

    public LiveProcessE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _testProjectPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "test-data", "MemoryPressureApp"
        );
    }

    [Fact]
    public async Task Full_Cycle_Attach_Collect_Analyze_MemoryPressureApp()
    {
        // Arrange - start MemoryPressureApp as child process
        _output.WriteLine($"Starting MemoryPressureApp from: {_testProjectPath}");

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project " + _testProjectPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        process.Start();

        // Wait for process to initialize and allocate memory
        await Task.Delay(5000); // Give it time to allocate memory

        var pid = process.Id;
        _output.WriteLine($"MemoryPressureApp started with PID: {pid}");

        try
        {
            // Setup services using DI container
            var options = new ClrScopeOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), $"clrscope-e2e-{Guid.NewGuid()}"),
                DatabasePath = Path.Combine(Path.GetTempPath(), $"clrscope-e2e-{Guid.NewGuid()}.db")
            };

            Directory.CreateDirectory(options.ArtifactRoot);

            var services = new ServiceCollection();
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
            services.AddLogging();
            services.AddClrScopeStorage();
            services.AddClrScopeDiagnostics();
            services.AddClrScopeCollectionServices();

            var serviceProvider = services.BuildServiceProvider();

            // Initialize database schema
            var schemaInitializer = serviceProvider.GetRequiredService<SqliteSchemaInitializer>();
            await schemaInitializer.InitializeAsync();

            var sessionStore = serviceProvider.GetRequiredService<ISqliteSessionStore>();
            var artifactStore = serviceProvider.GetRequiredService<ISqliteArtifactStore>();
            var collectGcDumpService = serviceProvider.GetRequiredService<CollectGcDumpService>();
            var runtimeService = new RuntimeService();
            var heapSnapshotPreparer = serviceProvider.GetRequiredService<IHeapSnapshotPreparer>();

            // Act - find the process
            _output.WriteLine("Listing .NET targets...");
            var targets = runtimeService.ListTargets();
            var target = targets.FirstOrDefault(t => t.Pid == pid);

            Assert.NotNull(target);
            _output.WriteLine($"Found target: {target.ProcessName} (PID: {target.Pid})");

            // Wait for MemoryPressureApp to allocate memory (it allocates 150MB in chunks)
            _output.WriteLine("Waiting for memory allocation...");
            await Task.Delay(TimeSpan.FromSeconds(15));

            // Collect GC dump
            _output.WriteLine("Collecting GC dump...");
            var request = new CollectGcDumpRequest(pid);
            var result = await collectGcDumpService.CollectGcDumpAsync(request, cancellationToken: CancellationToken.None);

            if (result.Error != null)
            {
                Assert.Fail($"GC dump collection failed: {result.Error}");
            }
            Assert.NotNull(result.Session);
            Assert.NotNull(result.Artifact);

            var artifactId = result.Artifact.ArtifactId.Value;
            _output.WriteLine($"GC dump collected: {artifactId}");

            // Analyze heap
            _output.WriteLine("Analyzing heap snapshot...");
            var artifact = await artifactStore.GetAsync(result.Artifact.ArtifactId, CancellationToken.None);
            Assert.NotNull(artifact);

            var heapOptions = new HeapPreparationOptions
            {
                Metric = HeapMetricKind.ShallowSize,
                AnalysisMode = HeapAnalysisMode.Auto,
                GroupBy = HeapGroupBy.Type,
                MaxTypes = 50
            };

            var prepared = await heapSnapshotPreparer.PrepareAsync(artifact, heapOptions, CancellationToken.None);
            Assert.NotNull(prepared);
            Assert.NotNull(prepared.Snapshot);

            _output.WriteLine($"Total heap bytes: {prepared.Snapshot.Metadata.TotalHeapBytes:N0}");
            _output.WriteLine($"Total object count: {prepared.Snapshot.Metadata.TotalObjectCount:N0}");
            _output.WriteLine($"Top 5 types by size:");
            foreach (var typeStat in prepared.Snapshot.TypeStats.Take(5))
            {
                _output.WriteLine($"  {typeStat.TypeName}: {typeStat.ShallowSizeBytes:N0} bytes ({typeStat.Count:N0} objects)");
            }

            // Assert - verify we have expected types from MemoryPressureApp
            var typeNames = prepared.Snapshot.TypeStats.Select(ts => ts.TypeName).ToList();
            
            // MemoryPressureApp creates byte[] arrays and System.Object instances
            Assert.Contains("System.Byte[]", typeNames);
            Assert.Contains("System.Object", typeNames);

            // Verify heap size is > 10MB (MemoryPressureApp allocates memory but actual heap size varies)
            Assert.True(prepared.Snapshot.Metadata.TotalHeapBytes > 10 * 1024 * 1024,
                $"Heap size should be > 10MB, got {prepared.Snapshot.Metadata.TotalHeapBytes / 1024 / 1024:N2}MB");

            // Cleanup
            Directory.Delete(options.ArtifactRoot, recursive: true);
            File.Delete(options.DatabasePath);
        }
        finally
        {
            // Cleanup - kill the process
            _output.WriteLine($"Killing process {pid}...");
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
    }

    public void Dispose()
    {
        // Cleanup handled in test finally blocks
    }
}
