using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Domain.Heap.Enums;
using ClrScope.Mcp.Domain.Heap.Options;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using ClrScope.Mcp.Services.Heap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClrScope.Mcp.Tools.Analysis;

// Layer 1: Artifact Inspection - determines artifact type and properties
internal interface IArtifactInspector
{
    ArtifactInspectionResult Inspect(Artifact artifact);
}

internal record ArtifactInspectionResult(
    ArtifactKind Kind,
    bool RequiresPreprocessing,
    string RecommendedFlameKind
);

// Layer 2: Preparation Layer - normalizes data for rendering
internal interface IStackDataPreparer
{
    Task<PreparedStackData> PrepareAsync(Artifact artifact, string flameKind, string analysisMode, CancellationToken cancellationToken, IProgress<AnalysisProgress>? progress = null);
}

internal enum AnalysisPhase
{
    Validating,
    PreparingSymbols,
    RunningAnalysis,
    ParsingStacks,
    Aggregating,
    Rendering,
    Completed
}

internal record AnalysisProgress(
    AnalysisPhase Phase,
    int CurrentStep,
    int TotalSteps,
    string Message
);

internal record PreparedStackData(
    List<StackFrameData> StackFrames,
    string Source,
    bool FromCache
);

// Layer 3: Rendering Layer - generates flame graph from normalized data
internal interface IFlameGraphRenderer
{
    string Render(PreparedStackData data, Artifact artifact, string format, string flameKind);
}

[McpServerToolType]
public sealed class SummaryTools
{
    private class DefaultArtifactInspector : IArtifactInspector
    {
        public ArtifactInspectionResult Inspect(Artifact artifact)
        {
            var recommendedFlameKind = artifact.Kind switch
            {
                ArtifactKind.Dump => "snapshot",
                ArtifactKind.Trace => "cpu",
                ArtifactKind.Stacks => "snapshot",
                _ => "snapshot"
            };

            var requiresPreprocessing = artifact.Kind == ArtifactKind.Dump || artifact.Kind == ArtifactKind.Trace;

            return new ArtifactInspectionResult(
                artifact.Kind,
                requiresPreprocessing,
                recommendedFlameKind
            );
        }
    }

    private static readonly IArtifactInspector _artifactInspector = new DefaultArtifactInspector();

    private class StackDataPreparer : IStackDataPreparer
    {
        private readonly ISosAnalyzer _sosAnalyzer;
        private readonly ILogger _logger;

        public StackDataPreparer(ISosAnalyzer sosAnalyzer, ILogger logger)
        {
            _sosAnalyzer = sosAnalyzer;
            _logger = logger;
        }

        public async Task<PreparedStackData> PrepareAsync(Artifact artifact, string flameKind, string analysisMode, CancellationToken cancellationToken, IProgress<AnalysisProgress>? progress = null)
        {
            var totalSteps = 5;
            var currentStep = 0;

            progress?.Report(new AnalysisProgress(AnalysisPhase.Validating, ++currentStep, totalSteps, "Validating artifact..."));

            var cacheKey = _stacksCache.GenerateCacheKey(artifact, analysisMode, flameKind);

            // Check cache based on analysis_mode
            List<StackFrameData>? stackFrames = null;
            if (analysisMode == "reuse")
            {
                progress?.Report(new AnalysisProgress(AnalysisPhase.Validating, ++currentStep, totalSteps, "Checking cache..."));
                if (_stacksCache.TryGet(cacheKey, out stackFrames) && stackFrames != null)
                {
                    progress?.Report(new AnalysisProgress(AnalysisPhase.Completed, totalSteps, totalSteps, "Loaded from cache"));
                    return new PreparedStackData(stackFrames, "cache", true);
                }
                progress?.Report(new AnalysisProgress(AnalysisPhase.Completed, totalSteps, totalSteps, "Cache miss"));
                return new PreparedStackData(new List<StackFrameData>(), "cache_miss", false);
            }
            else if (analysisMode == "force")
            {
                // Force re-analysis
                progress?.Report(new AnalysisProgress(AnalysisPhase.PreparingSymbols, ++currentStep, totalSteps, "Preparing for analysis..."));
                stackFrames = await ExtractStackFramesAsync(artifact, flameKind, cancellationToken, progress, currentStep, totalSteps);
                progress?.Report(new AnalysisProgress(AnalysisPhase.Aggregating, ++currentStep, totalSteps, "Caching results..."));
                _stacksCache.Set(cacheKey, stackFrames ?? new List<StackFrameData>());
                progress?.Report(new AnalysisProgress(AnalysisPhase.Completed, totalSteps, totalSteps, "Analysis complete"));
                return new PreparedStackData(stackFrames ?? new List<StackFrameData>(), "force_analysis", false);
            }
            else // auto
            {
                progress?.Report(new AnalysisProgress(AnalysisPhase.Validating, ++currentStep, totalSteps, "Checking cache..."));
                if (_stacksCache.TryGet(cacheKey, out stackFrames) && stackFrames != null)
                {
                    _logger.LogInformation("Using cached preprocessed stacks for artifact {ArtifactId}", artifact.ArtifactId);
                    progress?.Report(new AnalysisProgress(AnalysisPhase.Completed, totalSteps, totalSteps, "Loaded from cache"));
                    return new PreparedStackData(stackFrames, "cache", true);
                }
                else
                {
                    progress?.Report(new AnalysisProgress(AnalysisPhase.PreparingSymbols, ++currentStep, totalSteps, "Preparing for analysis..."));
                    stackFrames = await ExtractStackFramesAsync(artifact, flameKind, cancellationToken, progress, currentStep, totalSteps);
                    progress?.Report(new AnalysisProgress(AnalysisPhase.Aggregating, ++currentStep, totalSteps, "Caching results..."));
                    _stacksCache.Set(cacheKey, stackFrames ?? new List<StackFrameData>());
                    progress?.Report(new AnalysisProgress(AnalysisPhase.Completed, totalSteps, totalSteps, "Analysis complete"));
                    return new PreparedStackData(stackFrames ?? new List<StackFrameData>(), "analysis", false);
                }
            }
        }

        private async Task<List<StackFrameData>> ExtractStackFramesAsync(Artifact artifact, string flameKind, CancellationToken cancellationToken, IProgress<AnalysisProgress>? progress, int currentStep, int totalSteps)
        {
            if (artifact.Kind == ArtifactKind.Dump)
            {
                progress?.Report(new AnalysisProgress(AnalysisPhase.RunningAnalysis, currentStep + 1, totalSteps, "Running SOS analysis (clrstack -all)..."));
                return await ExtractStacksFromDump(artifact.FilePath, _sosAnalyzer, cancellationToken);
            }
            else if (artifact.Kind == ArtifactKind.Trace)
            {
                progress?.Report(new AnalysisProgress(AnalysisPhase.RunningAnalysis, currentStep + 1, totalSteps, "Parsing EventPipe trace..."));
                return ExtractStacksFromTrace(artifact.FilePath, flameKind);
            }
            else if (artifact.Kind == ArtifactKind.Stacks)
            {
                progress?.Report(new AnalysisProgress(AnalysisPhase.ParsingStacks, currentStep + 1, totalSteps, "Parsing stack data..."));
                // Direct parsing for Stacks
                if (File.Exists(artifact.FilePath))
                {
                    var fileContent = File.ReadAllText(artifact.FilePath);
                    if (artifact.FilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        var stacksOutput = System.Text.Json.JsonSerializer.Deserialize<StacksOutput>(fileContent);
                        if (stacksOutput != null && stacksOutput.Threads != null)
                        {
                            return ConvertToStackFrameData(stacksOutput);
                        }
                    }
                    else
                    {
                        return ParsePlainStackText(fileContent);
                    }
                }
            }

            return new List<StackFrameData>();
        }
    }

    private class FlameGraphRenderer : IFlameGraphRenderer
    {
        public string Render(PreparedStackData data, Artifact artifact, string format, string flameKind)
        {
            if (data.StackFrames == null || data.StackFrames.Count == 0)
            {
                return GeneratePlaceholderFlameGraph(artifact, format);
            }

            if (format == "svg")
            {
                return GenerateSvgFlameGraph(data.StackFrames, artifact, flameKind);
            }
            else
            {
                return GenerateHtmlFlameGraph(data.StackFrames, artifact, flameKind);
            }
        }
    }

    [McpServerTool(Name = "artifact_summarize"), Description("Analyze and summarize an artifact with findings and recommendations. Focus options: all, memory, threads, cpu, io")]
    public static async Task<ArtifactAnalysisResult> SummarizeArtifact(
        string artifactId,
        McpServer server,
        [Description("Analysis focus: 'all' (default), 'memory', 'threads', 'cpu', 'io'")] string focus = "all",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return ArtifactAnalysisResult.Failure($"Artifact not found: {artifactId}");
            }

            // Analyze the artifact based on its kind and focus
            var analysis = AnalyzeArtifactContent(artifact, logger, focus.ToLowerInvariant());
            return analysis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to summarize artifact: {ArtifactId}", artifactId);
            return ArtifactAnalysisResult.Failure($"Failed to summarize artifact: {ex.Message}");
        }
    }

    [McpServerTool(Name = "detect_patterns"), Description("Automatically detect common problem patterns (memory leaks, deadlocks, thread pool starvation, high CPU)")]
    public static async Task<PatternDetectionResult> DetectPatterns(
        string artifactId,
        McpServer server,
        [Description("Pattern types to detect: 'all' (default), 'memory_leaks', 'deadlocks', 'thread_pool', 'high_cpu', 'excessive_allocations'")] string patternTypes = "all",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var sosAnalyzer = server.Services!.GetRequiredService<ISosAnalyzer>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();
        var options = server.Services!.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClrScopeOptions>>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return PatternDetectionResult.Failure($"Artifact not found: {artifactId}");
            }

            // Validate artifact path is within artifact root
            var artifactRoot = options.Value.GetArtifactRoot();
            PathSecurity.EnsurePathWithinDirectory(artifact.FilePath, artifactRoot);

            var detectedPatterns = new List<DetectedPattern>();
            var patternsToDetect = patternTypes.ToLowerInvariant() == "all"
                ? new[] { "memory_leaks", "deadlocks", "thread_pool", "high_cpu", "excessive_allocations" }
                : patternTypes.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Detect patterns based on artifact kind
            switch (artifact.Kind)
            {
                case ArtifactKind.Dump:
                    // Check if dotnet-dump is available
                    if (!await sosAnalyzer.IsAvailableAsync(cancellationToken))
                    {
                        return PatternDetectionResult.Failure("dotnet-dump CLI not found. Pattern detection for Dump requires SOS commands.");
                    }
                    await DetectDumpPatterns(artifact.FilePath, sosAnalyzer, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                case ArtifactKind.GcDump:
                    // Check if dotnet-dump is available
                    if (!await sosAnalyzer.IsAvailableAsync(cancellationToken))
                    {
                        return PatternDetectionResult.Failure("dotnet-dump CLI not found. Pattern detection for GcDump requires SOS commands.");
                    }
                    await DetectGCDumpPatterns(artifact.FilePath, sosAnalyzer, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                case ArtifactKind.Trace:
                    await DetectTracePatterns(artifact.FilePath, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                case ArtifactKind.Counters:
                    await DetectCountersPatterns(artifact.FilePath, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                case ArtifactKind.Stacks:
                    await DetectStacksPatterns(artifact.FilePath, patternsToDetect, detectedPatterns, cancellationToken);
                    break;
                default:
                    return PatternDetectionResult.Failure($"Pattern detection not supported for artifact kind: {artifact.Kind}");
            }

            return PatternDetectionResult.Success(detectedPatterns.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pattern detection failed for artifact {ArtifactId}", artifactId);
            return PatternDetectionResult.Failure($"Pattern detection failed: {ex.Message}");
        }
    }

    [McpServerTool(Name = "visualize_flame_graph"), Description("Generate flame graph visualization from stack traces. Supports Stacks artifacts directly, Dump/Trace require auto_analyze")]
    public static async Task<VisualizationResult> VisualizeFlameGraph(
        string artifactId,
        McpServer server,
        [Description("Visualization format: 'svg' (default), 'html'")] string format = "svg",
        [Description("Auto-analyze artifact: 'true' (default) for Dump/Trace preprocessing, 'false' for pre-processed data")] string autoAnalyze = "true",
        [Description("Analysis mode: 'auto' (default), 'reuse' (cache only), 'force' (re-analyze)")] string analysisMode = "auto",
        [Description("Flame graph kind: 'auto' (default), 'cpu' (CPU sampling), 'snapshot' (process snapshot for Dump), 'allocation' (memory allocation)")] string flameKind = "auto",
        [Description("Optional filename to save visualization. If provided, file will be saved and opened in default browser")] string filename = "",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();
        var options = server.Services!.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClrScopeOptions>>();
        var sosAnalyzer = server.Services!.GetRequiredService<ISosAnalyzer>();

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return VisualizationResult.Failure($"Artifact not found: {artifactId}");
            }

            // Validate artifact path is within artifact root
            var artifactRoot = options.Value.GetArtifactRoot();
            PathSecurity.EnsurePathWithinDirectory(artifact.FilePath, artifactRoot);

            // Parse parameters
            var enableAutoAnalyze = autoAnalyze.ToLowerInvariant() == "true";
            var mode = analysisMode.ToLowerInvariant();
            var kind = flameKind.ToLowerInvariant();

            // Layer 1: Artifact Inspection
            var inspection = _artifactInspector.Inspect(artifact);

            // Determine flame kind based on artifact type if auto
            if (kind == "auto")
            {
                kind = inspection.RecommendedFlameKind;
            }

            // Validate flame kind compatibility with artifact type
            if (artifact.Kind == ArtifactKind.Dump && kind == "cpu")
            {
                return VisualizationResult.Failure($"Dump artifacts do not support CPU flame graph. Use 'snapshot' flame kind for process snapshots, got: {kind}");
            }

            // For Stacks artifacts, use direct parsing without preprocessing
            if (artifact.Kind == ArtifactKind.Stacks)
            {
                var stacksFlameGraph = GenerateFlameGraph(artifact, format.ToLowerInvariant(), kind);
                
                // Save to file if filename is provided
                if (!string.IsNullOrEmpty(filename))
                {
                    await SaveAndOpenVisualizationAsync(stacksFlameGraph, filename, format.ToLowerInvariant(), logger, artifactRoot, cancellationToken);
                }
                
                return VisualizationResult.Success(stacksFlameGraph, format.ToLowerInvariant());
            }

            // For Dump and Trace artifacts, require auto_analyze
            if (!enableAutoAnalyze)
            {
                return VisualizationResult.Failure($"Artifact kind {artifact.Kind} requires auto_analyze=true for preprocessing. Set auto_analyze=true or pre-process the artifact manually.");
            }

            // Layer 2: Preparation Layer
            var preparer = new StackDataPreparer(sosAnalyzer, logger);
            var progress = new Progress<AnalysisProgress>(p =>
            {
                logger.LogInformation("Analysis progress: [{Phase}] Step {CurrentStep}/{TotalSteps} - {Message}",
                    p.Phase, p.CurrentStep, p.TotalSteps, p.Message);
            });
            var preparedData = await preparer.PrepareAsync(artifact, kind, mode, cancellationToken, progress);

            if (preparedData.StackFrames == null || preparedData.StackFrames.Count == 0)
            {
                if (artifact.Kind == ArtifactKind.Dump)
                {
                    return VisualizationResult.Failure("Failed to extract stacks from dump artifact. SOS command 'clrstack -all' returned no data.");
                }
                else if (artifact.Kind == ArtifactKind.Trace)
                {
                    return VisualizationResult.Failure("Failed to extract stacks from trace artifact. Trace file may not contain CPU sampling data. Collect trace with 'dotnet-trace collect' using default profiles or --profile dotnet-sampled-thread-time,dotnet-common.");
                }
                else
                {
                    return VisualizationResult.Failure("Failed to extract stack data from artifact.");
                }
            }

            // Layer 3: Rendering Layer
            var renderer = new FlameGraphRenderer();
            var flameGraph = renderer.Render(preparedData, artifact, format.ToLowerInvariant(), kind);

            // Save to file if filename is provided
            if (!string.IsNullOrEmpty(filename))
            {
                await SaveAndOpenVisualizationAsync(flameGraph, filename, format.ToLowerInvariant(), logger, artifactRoot, cancellationToken);
            }

            return VisualizationResult.Success(flameGraph, format.ToLowerInvariant());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flame graph visualization failed for artifact {ArtifactId}", artifactId);
            return VisualizationResult.Failure($"Flame graph visualization failed: {ex.Message}");
        }
    }

    private static async Task SaveAndOpenVisualizationAsync(string content, string filename, string format, ILogger logger, string artifactRoot, CancellationToken cancellationToken)
    {
        try
        {
            // Validate filename is within artifact root to prevent arbitrary file writes
            PathSecurity.EnsurePathWithinDirectory(filename, artifactRoot);

            // Save to file
            await System.IO.File.WriteAllTextAsync(filename, content, cancellationToken);
            logger.LogInformation("Visualization saved to {Filename}", filename);

            // Open in default browser
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = filename,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
            logger.LogInformation("Visualization opened in default browser");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save or open visualization file {Filename}", filename);
            throw new InvalidOperationException($"Failed to save or open visualization: {ex.Message}", ex);
        }
    }

    private static List<StackFrameData> ExtractStacksFromTrace(string traceFilePath, string flameKind)
{
    // TODO: Implement proper TraceEvent library integration
    // TraceEvent library has complex API requiring proper configuration
    // For now, return empty list - will be implemented in future iteration
    // Alternative: use dotnet-trace convert --format Speedscope as fallback

    // Basic detection: check if trace file exists and has reasonable size
    if (!File.Exists(traceFilePath))
    {
        return new List<StackFrameData>();
    }

    var fileInfo = new FileInfo(traceFilePath);
    if (fileInfo.Length == 0)
    {
        return new List<StackFrameData>();
    }

    // .nettrace is a binary format - cannot use ReadAllText
    // This requires TraceEvent library or dotnet-trace convert for proper parsing
    // Returning empty list to trigger appropriate error message
    return new List<StackFrameData>();
}

private static async Task<List<StackFrameData>> ExtractStacksFromDump(string dumpFilePath, ISosAnalyzer sosAnalyzer, CancellationToken cancellationToken)
{
    try
    {
        // Execute clrstack -all to get stacks from all managed threads
        var result = await sosAnalyzer.ExecuteCommandAsync(dumpFilePath, "clrstack -all", cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return new List<StackFrameData>();
        }

        // Parse SOS output to extract stack frames
        return ParseSosClrstackOutput(result.Output);
    }
    catch (Exception)
    {
        return new List<StackFrameData>();
    }
}

private static List<StackFrameData> ParseSosClrstackOutput(string sosOutput)
{
    var frames = new List<StackFrameData>();
    var lines = sosOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var threadId = 0;
    var frameIndex = 0;

    foreach (var line in lines)
    {
        var trimmedLine = line.Trim();

        // Parse thread header: "OS Thread Id: 0x1234 (1234)"
        if (trimmedLine.StartsWith("OS Thread Id:") || trimmedLine.Contains("Thread"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var tid))
            {
                threadId = tid;
            }
            frameIndex = 0;
            continue;
        }

        // Parse stack frame lines
        // Format: "Child SP IP CallSite"
        // Example: "00007FF8A1E2E690 00007FF8A1E2E690 System.Threading.Tasks.Task.Execute()"
        if (!string.IsNullOrWhiteSpace(trimmedLine) &&
            !trimmedLine.StartsWith("OS Thread") &&
            !trimmedLine.StartsWith("Child SP") &&
            !trimmedLine.Contains("Unable to walk"))
        {
            var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                var callSite = string.Join(" ", parts.Skip(2));
                if (!string.IsNullOrWhiteSpace(callSite))
                {
                    frames.Add(new StackFrameData
                    {
                        ThreadId = threadId,
                        FrameIndex = frameIndex++,
                        CallSite = callSite
                    });
                }
            }
        }
    }

    return frames;
}

private static string GenerateFlameGraph(Artifact artifact, string format, string flameKind = "snapshot")
    {
        var flameGraphData = new System.Text.StringBuilder();
        List<StackFrameData>? stackFrames = null;

        try
        {
            // Try to read and parse stack data from artifact
            if (File.Exists(artifact.FilePath))
            {
                var fileContent = File.ReadAllText(artifact.FilePath);

                if (artifact.FilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse JSON format using the real StacksOutput structure
                    var stacksOutput = System.Text.Json.JsonSerializer.Deserialize<StacksOutput>(fileContent);
                    if (stacksOutput != null && stacksOutput.Threads != null)
                    {
                        stackFrames = ConvertToStackFrameData(stacksOutput);
                    }
                }
                else
                {
                    // Parse plain text format
                    stackFrames = ParsePlainStackText(fileContent);
                }
            }
        }
        catch (Exception)
        {
            // If parsing fails, use placeholder
        }

        if (stackFrames == null || stackFrames.Count == 0)
        {
            // Fallback to placeholder if no data available
            return GeneratePlaceholderFlameGraph(artifact, format);
        }

        // Generate real flame graph from parsed data
        if (format == "svg")
        {
            return GenerateSvgFlameGraph(stackFrames, artifact, flameKind);
        }
        else
        {
            return GenerateHtmlFlameGraph(stackFrames, artifact, flameKind);
        }
    }

    private static List<StackFrameData> ConvertToStackFrameData(StacksOutput stacksOutput)
    {
        var frames = new List<StackFrameData>();
        int frameIndex = 0;

        foreach (var thread in stacksOutput.Threads)
        {
            foreach (var frame in thread.Frames)
            {
                frames.Add(new StackFrameData
                {
                    ThreadId = thread.ThreadId,
                    FrameIndex = frameIndex++,
                    CallSite = frame.CallSite
                });
            }
        }

        return frames;
    }

    private static string GeneratePlaceholderFlameGraph(Artifact artifact, string format)
    {
        var flameGraphData = new System.Text.StringBuilder();
        var reason = GetParsingFailureReason(artifact);

        if (format == "svg")
        {
            flameGraphData.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            flameGraphData.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"600\">");
            flameGraphData.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#f0f0f0\"/>");
            flameGraphData.AppendLine("  <text x=\"10\" y=\"30\" font-family=\"Arial\" font-size=\"14\" fill=\"#333\">");
            flameGraphData.AppendLine($"    Flame Graph for {artifact.ArtifactId.Value} ({artifact.Kind})");
            flameGraphData.AppendLine("  </text>");
            flameGraphData.AppendLine("  <text x=\"10\" y=\"50\" font-family=\"Arial\" font-size=\"12\" fill=\"#666\">");
            flameGraphData.AppendLine($"    {reason}");
            flameGraphData.AppendLine("  </text>");
            flameGraphData.AppendLine("  <text x=\"10\" y=\"70\" font-family=\"Arial\" font-size=\"11\" fill=\"#888\">");
            flameGraphData.AppendLine("    For Stacks: Ensure JSON or plain text format is used");
            flameGraphData.AppendLine("  </text>");
            flameGraphData.AppendLine("  <text x=\"10\" y=\"85\" font-family=\"Arial\" font-size=\"11\" fill=\"#888\">");
            flameGraphData.AppendLine("    For Trace: Ensure CPU sampling profile was used");
            flameGraphData.AppendLine("  </text>");
            flameGraphData.AppendLine("  <text x=\"10\" y=\"100\" font-family=\"Arial\" font-size=\"11\" fill=\"#888\">");
            flameGraphData.AppendLine("    For Dump: Use 'clrstack' SOS command to extract stacks first");
            flameGraphData.AppendLine("  </text>");
            flameGraphData.AppendLine("</svg>");
        }
        else // html
        {
            flameGraphData.AppendLine("<!DOCTYPE html>");
            flameGraphData.AppendLine("<html>");
            flameGraphData.AppendLine("<head>");
            flameGraphData.AppendLine("  <title>Flame Graph - " + artifact.ArtifactId.Value + "</title>");
            flameGraphData.AppendLine("  <style>");
            flameGraphData.AppendLine("    body { font-family: Arial, sans-serif; margin: 20px; }");
            flameGraphData.AppendLine("    .reason { color: #666; margin: 10px 0; }");
            flameGraphData.AppendLine("    .suggestions { color: #888; font-size: 12px; margin: 5px 0; }");
            flameGraphData.AppendLine("  </style>");
            flameGraphData.AppendLine("</head>");
            flameGraphData.AppendLine("<body>");
            flameGraphData.AppendLine("  <h1>Flame Graph for " + artifact.ArtifactId.Value + " (" + artifact.Kind + ")</h1>");
            flameGraphData.AppendLine($"  <p class=\"reason\">{reason}</p>");
            flameGraphData.AppendLine("  <div class=\"suggestions\">");
            flameGraphData.AppendLine("    <p><strong>Suggestions:</strong></p>");
            flameGraphData.AppendLine("    <ul>");
            flameGraphData.AppendLine("      <li>For Stacks: Ensure JSON or plain text format is used</li>");
            flameGraphData.AppendLine("      <li>For Trace: Ensure CPU sampling profile was used during collection</li>");
            flameGraphData.AppendLine("      <li>For Dump: Use 'clrstack' SOS command to extract stacks first</li>");
            flameGraphData.AppendLine("      <li>For Counters/GcDump: Flame graph not supported for this artifact type</li>");
            flameGraphData.AppendLine("    </ul>");
            flameGraphData.AppendLine("  </div>");
            flameGraphData.AppendLine("</body>");
            flameGraphData.AppendLine("</html>");
        }

        return flameGraphData.ToString();
    }

    private static string GetParsingFailureReason(Artifact artifact)
    {
        if (!File.Exists(artifact.FilePath))
        {
            return "Artifact file not found - may have been deleted or path is incorrect";
        }

        try
        {
            var fileInfo = new FileInfo(artifact.FilePath);
            if (fileInfo.Length == 0)
            {
                return "Artifact file is empty - collection may have failed";
            }

            switch (artifact.Kind)
            {
                case ArtifactKind.Stacks:
                    return "Stack data could not be parsed - ensure valid JSON or plain text format";
                case ArtifactKind.Trace:
                    return "Trace artifacts require EventPipe binary format parsing - use dotnet-trace analyze tool for flame graphs";
                case ArtifactKind.Dump:
                    return "Dump artifacts require SOS analysis - use 'analyze_dump_sos' with 'clrstack' command to extract stacks first";
                case ArtifactKind.Counters:
                    return "Counters do not contain stack data - flame graph not applicable";
                case ArtifactKind.GcDump:
                    return "GcDump contains heap graph data rather than call stacks. Use visualize_heap_snapshot.";
                default:
                    return "No stack data available for this artifact type";
            }
        }
        catch (Exception)
        {
            return "Artifact file could not be read - check file permissions";
        }
    }

    private static List<StackFrameData> ParsePlainStackText(string text)
    {
        var frames = new List<StackFrameData>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var threadId = 0;
        var frameIndex = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("Thread "))
            {
                var match = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"Thread (\d+)");
                if (match.Success)
                {
                    threadId = int.Parse(match.Groups[1].Value);
                }
            }
            else if (!string.IsNullOrWhiteSpace(trimmedLine) && !trimmedLine.StartsWith("Child SP"))
            {
                var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var callSite = string.Join(" ", parts.Skip(2));
                    frames.Add(new StackFrameData
                    {
                        ThreadId = threadId,
                        FrameIndex = frameIndex++,
                        CallSite = callSite
                    });
                }
            }
        }

        return frames;
    }

    private static string GenerateSvgFlameGraph(List<StackFrameData> frames, Artifact artifact, string flameKind = "snapshot")
    {
        var svg = new System.Text.StringBuilder();
        var colors = new[] { "#ff6b6b", "#4ecdc4", "#45b7d1", "#96ceb4", "#ffeaa7", "#dfe6e9", "#fd79a8", "#a29bfe" };

        // Use compact width for better readability
        var width = 1200;
        var height = Math.Min(600, frames.Count * 25 + 100);

        svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"" + width + "\" height=\"" + height + "\">");
        svg.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#f8f9fa\"/>");
        svg.AppendLine("  <text x=\"10\" y=\"30\" font-family=\"Arial\" font-size=\"14\" fill=\"#2d3436\">");
        svg.AppendLine("    " + (flameKind == "cpu" ? "CPU" : "Snapshot") + " Flame Graph for " + artifact.ArtifactId.Value + " (" + frames.Count + " frames)");
        svg.AppendLine("  </text>");

        // Group frames by thread
        var threadGroups = frames.GroupBy(f => f.ThreadId).ToList();
        var y = 50;
        var frameHeight = 20;

        foreach (var threadGroup in threadGroups)
        {
            var threadFrames = threadGroup.ToList();

            // Get unique call sites sorted by frequency
            var uniqueCallSites = threadFrames
                .GroupBy(f => f.CallSite)
                .Select(g => new { CallSite = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            var x = 10;
            var frameWidth = 200; // Fixed width for each frame

            foreach (var callSite in uniqueCallSites)
            {
                var color = colors[threadGroup.Key % colors.Length];
                var displayText = TruncateCallSite(callSite.CallSite, 30);

                svg.AppendLine("  <rect x=\"" + x + "\" y=\"" + y + "\" width=\"" + frameWidth + "\" height=\"" + frameHeight + "\" fill=\"" + color + "\" rx=\"2\"/>");
                svg.AppendLine("  <text x=\"" + (x + 5) + "\" y=\"" + (y + 14) + "\" font-family=\"Arial\" font-size=\"10\" fill=\"white\">" + EscapeXml(displayText) + "</text>");

                x += frameWidth + 10;

                // Wrap to next line if too wide
                if (x + frameWidth > width)
                {
                    x = 10;
                    y += frameHeight + 5;
                }
            }

            y += frameHeight + 15;
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string GenerateHtmlFlameGraph(List<StackFrameData> frames, Artifact artifact, string flameKind = "snapshot")
    {
        var html = new System.Text.StringBuilder();
        var colors = new[] { "#ff6b6b", "#4ecdc4", "#45b7d1", "#96ceb4", "#ffeaa7", "#dfe6e9", "#fd79a8", "#a29bfe" };

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("  <title>" + (flameKind == "cpu" ? "CPU" : "Snapshot") + " Flame Graph - " + artifact.ArtifactId.Value + "</title>");
        html.AppendLine("  <style>");
        html.AppendLine("    body { font-family: Arial, sans-serif; margin: 20px; background-color: #f8f9fa; }");
        html.AppendLine("    .header { margin-bottom: 20px; }");
        html.AppendLine("    .thread-section { margin: 10px 0; }");
        html.AppendLine("    .thread-title { font-weight: bold; margin: 5px 0; color: #2d3436; }");
        html.AppendLine("    .frame-container { display: flex; flex-wrap: wrap; gap: 2px; }");
        html.AppendLine("    .frame { padding: 4px 8px; border-radius: 3px; color: white; font-size: 11px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <div class=\"header\">");
        html.AppendLine("    <h1>Flame Graph for " + artifact.ArtifactId.Value + "</h1>");
        html.AppendLine("    <p>Total frames: " + frames.Count + "</p>");
        html.AppendLine("  </div>");

        // Group frames by thread
        var threadGroups = frames.GroupBy(f => f.ThreadId);

        foreach (var threadGroup in threadGroups)
        {
            var threadFrames = threadGroup.ToList();
            var color = colors[threadGroup.Key % colors.Length];

            html.AppendLine("  <div class=\"thread-section\">");
            html.AppendLine("    <div class=\"thread-title\">Thread " + threadGroup.Key + " (" + threadFrames.Count + " frames)</div>");
            html.AppendLine("    <div class=\"frame-container\">");

            foreach (var frame in threadFrames)
            {
                var displayText = TruncateCallSite(frame.CallSite, 50);
                html.AppendLine("      <div class=\"frame\" style=\"background-color: " + color + ";\" title=\"" + frame.CallSite + "\">" + displayText + "</div>");
            }

            html.AppendLine("    </div>");
            html.AppendLine("  </div>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private class PreprocessedStacksCache
{
    private readonly Dictionary<string, List<StackFrameData>> _cache = new();
    private readonly object _lock = new();

    public bool TryGet(string key, out List<StackFrameData>? value)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(key, out value);
        }
    }

    public void Set(string key, List<StackFrameData> value)
    {
        lock (_lock)
        {
            _cache[key] = value;
        }
    }

    public string GenerateCacheKey(Artifact artifact, string analysisMode, string flameKind)
    {
        // Key: artifact_hash + artifact_kind + analysis_backend_version + command_profile + symbol_context + runtime_identity
        var hash = ComputeFileHash(artifact.FilePath);
        var backendVersion = "1.0"; // SOS/TraceEvent version
        var commandProfile = flameKind == "cpu" ? "cpu" : "snapshot";

        return $"{hash}_{artifact.Kind}_{backendVersion}_{commandProfile}_{analysisMode}";
    }

    private string ComputeFileHash(string filePath)
    {
        try
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
        }
        catch
        {
            return $"fallback_{Path.GetFileName(filePath)}_{File.GetLastWriteTimeUtc(filePath):yyyyMMddHHmmss}";
        }
    }
}

private static readonly PreprocessedStacksCache _stacksCache = new();

private static string TruncateCallSite(string callSite, int maxLength)
    {
        if (string.IsNullOrEmpty(callSite) || callSite.Length <= maxLength)
            return callSite;
        if (maxLength < 4)
            return "...";
        return callSite.Substring(0, maxLength - 3) + "...";
    }

    private static async Task DetectDumpPatterns(string filePath, ISosAnalyzer sosAnalyzer, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        foreach (var pattern in patternsToDetect)
        {
            switch (pattern)
            {
                case "memory_leaks":
                    await DetectMemoryLeaks(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "deadlocks":
                    await DetectDeadlocks(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "thread_pool":
                    await DetectThreadPoolIssues(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "high_cpu":
                    await DetectHighCPU(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "excessive_allocations":
                    await DetectExcessiveAllocations(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
            }
        }
    }

    private static async Task DetectGCDumpPatterns(string filePath, ISosAnalyzer sosAnalyzer, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        foreach (var pattern in patternsToDetect)
        {
            switch (pattern)
            {
                case "memory_leaks":
                    await DetectGCDumpMemoryLeaks(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
                case "excessive_allocations":
                    await DetectGCDumpAllocations(filePath, sosAnalyzer, detectedPatterns, cancellationToken);
                    break;
            }
        }
    }

    private static async Task DetectMemoryLeaks(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "dumpheap -stat", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        // Analyze for memory leak indicators
        if (output.Contains("Large Object Heap") && output.Contains("MB"))
        {
            var lohSize = ExtractSizeFromOutput(output, "Large Object Heap");
            if (lohSize > 100 * 1024 * 1024) // > 100MB
            {
                detectedPatterns.Add(new DetectedPattern(
                    PatternType: "memory_leak",
                    Severity: "high",
                    Description: $"Large Object Heap size is {FormatBytes(lohSize)}, indicating potential memory leak",
                    Recommendation: "Investigate large objects in LOH using 'dumpheap -mt <MethodTable>' and check for pinned objects"
                ));
            }
        }

        // Check for gen2 growth
        if (output.Contains("Gen 2"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "medium",
                Description: "Gen 2 heap statistics available - review for long-lived objects",
                Recommendation: "Use 'dumpheap -stat' and 'gcroot' to identify roots of long-lived objects"
            ));
        }
    }

    private static async Task DetectDeadlocks(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "threads", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        var threadLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var blockedThreads = 0;

        foreach (var line in threadLines)
        {
            if (line.Contains("Blocked") || line.Contains("Wait"))
            {
                blockedThreads++;
            }
        }

        if (blockedThreads > 5)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "deadlock",
                Severity: "high",
                Description: $"{blockedThreads} threads appear blocked, potential deadlock",
                Recommendation: "Use 'clrstack' on blocked threads to identify circular wait patterns"
            ));
        }
    }

    private static async Task DetectThreadPoolIssues(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "threadpool", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        if (output.Contains("Work") && output.Contains("min") && output.Contains("max"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "medium",
                Description: "Thread pool statistics available - review for starvation",
                Recommendation: "Check thread pool queue length and worker thread utilization"
            ));
        }
    }

    private static async Task DetectHighCPU(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "threads", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        if (output.Contains("Running") || output.Contains("Preemptive"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "medium",
                Description: "Threads in running state detected - review for CPU hotspots",
                Recommendation: "Use 'clrstack' on running threads to identify CPU-intensive methods"
            ));
        }
    }

    private static async Task DetectExcessiveAllocations(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        var result = await sosAnalyzer.ExecuteCommandAsync(filePath, "dumpheap -stat", cancellationToken);
        if (!result.Success) return;

        var output = result.Output;
        // Look for large allocation counts
        if (output.Contains("Count") && output.Contains("Total"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "excessive_allocations",
                Severity: "medium",
                Description: "Heap statistics available - review allocation patterns",
                Recommendation: "Check top types by count to identify excessive allocations"
            ));
        }
    }

    private static async Task DetectGCDumpMemoryLeaks(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        detectedPatterns.Add(new DetectedPattern(
            PatternType: "memory_leak",
            Severity: "medium",
            Description: "GC dump analysis for memory leaks requires dotnet-gcdump-analyzer",
            Recommendation: "Use dotnet-gcdump-analyze to examine heap growth and object references"
        ));
    }

    private static async Task DetectGCDumpAllocations(string filePath, ISosAnalyzer sosAnalyzer, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        detectedPatterns.Add(new DetectedPattern(
            PatternType: "excessive_allocations",
            Severity: "medium",
            Description: "GC dump allocation analysis requires dotnet-gcdump-analyzer",
            Recommendation: "Use dotnet-gcdump-analyzer to examine allocation rates and patterns"
        ));
    }

    private static async Task DetectTracePatterns(string filePath, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            // .nettrace is a binary format - cannot use ReadAllTextAsync
            // This requires TraceEvent library or dotnet-trace convert for proper parsing
            // Pattern detection not available for binary trace files
            return;
        }
        catch (Exception)
        {
            // If trace file cannot be read, skip pattern detection
        }
    }

    private static void DetectTraceHighCPU(string fileContent, long traceSize, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("CPU") || fileContent.Contains("Sample") || fileContent.Contains("cpu-sampling"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "medium",
                Description: "Trace contains CPU sampling data - analyze for CPU hotspots",
                Recommendation: "Use PerfView or dotnet-trace analyze to identify hot methods with high CPU time"
            ));
        }

        if (traceSize > 10 * 1024 * 1024)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "low",
                Description: $"Large trace file ({FormatBytes(traceSize)}) - may indicate long CPU-intensive period",
                Recommendation: "Review trace duration and focus on period of high CPU activity"
            ));
        }
    }

    private static void DetectTraceMemoryLeaks(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("GC") || fileContent.Contains("Heap") || fileContent.Contains("gc-heap"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "medium",
                Description: "Trace contains GC/heap events - analyze for memory growth patterns",
                Recommendation: "Check GC pause times, heap size growth, and allocation rates over time"
            ));
        }

        if (fileContent.Contains("Gen2") || fileContent.Contains("LOH") || fileContent.Contains("Large Object"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "high",
                Description: "Trace contains Gen2/LOH activity - potential memory leak indicators",
                Recommendation: "Examine long-lived objects in Gen2 and Large Object Heap for leak sources"
            ));
        }
    }

    private static void DetectTraceThreadPool(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("ThreadPool") || fileContent.Contains("Worker") || fileContent.Contains("IO"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "medium",
                Description: "Trace contains thread pool events - analyze for starvation or contention",
                Recommendation: "Check thread pool queue length, worker thread utilization, and I/O completion port usage"
            ));
        }

        if (fileContent.Contains("Starvation") || fileContent.Contains("Exhaustion") || fileContent.Contains("Queue"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "high",
                Description: "Trace indicates potential thread pool starvation or queue buildup",
                Recommendation: "Review thread pool configuration and adjust min/max threads if needed"
            ));
        }
    }

    private static void DetectTraceAllocations(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("Alloc") || fileContent.Contains("Allocation"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "excessive_allocations",
                Severity: "medium",
                Description: "Trace contains allocation events - analyze for allocation hotspots",
                Recommendation: "Identify methods with high allocation rates and consider object pooling or reuse"
            ));
        }
    }

    private static async Task DetectCountersPatterns(string filePath, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);

            foreach (var pattern in patternsToDetect)
            {
                switch (pattern)
                {
                    case "high_cpu":
                        DetectCountersHighCPU(fileContent, detectedPatterns);
                        break;
                    case "memory_leaks":
                        DetectCountersMemoryLeaks(fileContent, detectedPatterns);
                        break;
                    case "thread_pool":
                        DetectCountersThreadPool(fileContent, detectedPatterns);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // If counters file cannot be read, skip pattern detection
        }
    }

    private static void DetectCountersHighCPU(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("cpu-usage") || fileContent.Contains("process-cpu") || fileContent.Contains("% Processor Time"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "medium",
                Description: "Counters contain CPU usage metrics - analyze for high CPU periods",
                Recommendation: "Review CPU usage over time and identify periods of sustained high CPU"
            ));
        }
    }

    private static void DetectCountersMemoryLeaks(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("memory") || fileContent.Contains("gc-heap") || fileContent.Contains("working-set"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "medium",
                Description: "Counters contain memory metrics - analyze for memory growth patterns",
                Recommendation: "Check for steady memory growth over time indicating potential memory leak"
            ));
        }

        if (fileContent.Contains("gen-2") || fileContent.Contains("loh"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "memory_leak",
                Severity: "high",
                Description: "Counters show Gen2/LOH activity - potential memory leak indicators",
                Recommendation: "Monitor Gen2 and LOH size over time for steady growth patterns"
            ));
        }
    }

    private static void DetectCountersThreadPool(string fileContent, List<DetectedPattern> detectedPatterns)
    {
        if (fileContent.Contains("threadpool") || fileContent.Contains("worker-threads") || fileContent.Contains("io-threads"))
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "medium",
                Description: "Counters contain thread pool metrics - analyze for starvation",
                Recommendation: "Check thread pool queue length and thread utilization for signs of starvation"
            ));
        }
    }

    private static async Task DetectStacksPatterns(string filePath, string[] patternsToDetect, List<DetectedPattern> detectedPatterns, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pattern in patternsToDetect)
            {
                switch (pattern)
                {
                    case "deadlocks":
                        DetectStacksDeadlocks(lines, detectedPatterns);
                        break;
                    case "thread_pool":
                        DetectStacksThreadPool(lines, detectedPatterns);
                        break;
                    case "high_cpu":
                        DetectStacksHighCPU(lines, detectedPatterns);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // If stacks file cannot be read, skip pattern detection
        }
    }

    private static void DetectStacksDeadlocks(string[] lines, List<DetectedPattern> detectedPatterns)
    {
        var blockedThreads = 0;
        var threadIds = new HashSet<int>();

        foreach (var line in lines)
        {
            if (line.Contains("Blocked") || line.Contains("Wait") || line.Contains("Lock"))
            {
                blockedThreads++;
            }

            var match = System.Text.RegularExpressions.Regex.Match(line, @"Thread (\d+)");
            if (match.Success)
            {
                threadIds.Add(int.Parse(match.Groups[1].Value));
            }
        }

        if (blockedThreads > 5)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "deadlock",
                Severity: "high",
                Description: $"{blockedThreads} threads appear blocked, potential deadlock",
                Recommendation: "Examine stack frames of blocked threads to identify circular wait patterns and lock contention"
            ));
        }

        if (threadIds.Count > 0)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "deadlock",
                Severity: "low",
                Description: $"Stacks contain {threadIds.Count} threads - review for blocking patterns",
                Recommendation: "Check thread states and identify threads waiting on synchronization primitives"
            ));
        }
    }

    private static void DetectStacksThreadPool(string[] lines, List<DetectedPattern> detectedPatterns)
    {
        var threadPoolThreads = 0;

        foreach (var line in lines)
        {
            if (line.Contains("ThreadPool") || line.Contains("Worker Thread") || line.Contains(".NET ThreadPool"))
            {
                threadPoolThreads++;
            }
        }

        if (threadPoolThreads > 0)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "thread_pool",
                Severity: "medium",
                Description: $"Stacks contain {threadPoolThreads} thread pool threads - analyze for starvation",
                Recommendation: "Review thread pool thread states and queue length for signs of thread pool exhaustion"
            ));
        }
    }

    private static void DetectStacksHighCPU(string[] lines, List<DetectedPattern> detectedPatterns)
    {
        var runningThreads = 0;

        foreach (var line in lines)
        {
            if (line.Contains("Running") || line.Contains("Preemptive") || line.Contains("CPU"))
            {
                runningThreads++;
            }
        }

        if (runningThreads > 5)
        {
            detectedPatterns.Add(new DetectedPattern(
                PatternType: "high_cpu",
                Severity: "high",
                Description: $"{runningThreads} threads in running state - potential CPU hotspot",
                Recommendation: "Identify hot methods appearing frequently across running thread stacks"
            ));
        }
    }

    private static long ExtractSizeFromOutput(string output, string keyword)
    {
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains(keyword))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[^1], out var size))
                {
                    return size;
                }
            }
        }
        return 0;
    }

    private static ArtifactAnalysisResult AnalyzeArtifactContent(Artifact artifact, ILogger logger, string focus = "all")
    {
        try
        {
            var summary = new ArtifactAnalysisSummary(
                ArtifactId: artifact.ArtifactId.Value,
                Kind: artifact.Kind.ToString(),
                SizeBytes: artifact.SizeBytes,
                Status: artifact.Status.ToString(),
                Pid: artifact.Pid,
                CreatedAt: artifact.CreatedAtUtc,
                FilePath: artifact.FilePath,
                KeyMetrics: new Dictionary<string, string>(),
                Findings: new List<string>(),
                Recommendations: new List<string>()
            );

            // Add basic metrics
            summary.KeyMetrics["Size"] = FormatBytes(artifact.SizeBytes);
            summary.KeyMetrics["Status"] = artifact.Status.ToString();
            summary.KeyMetrics["Process ID"] = artifact.Pid.ToString();
            summary.KeyMetrics["Created"] = artifact.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss");
            summary.KeyMetrics["Analysis Focus"] = focus;

            // Analyze based on kind and focus
            switch (artifact.Kind)
            {
                case ArtifactKind.Stacks:
                    AnalyzeStacksArtifact(artifact, summary, focus);
                    break;
                case ArtifactKind.Dump:
                    AnalyzeDumpArtifact(artifact, summary, focus);
                    break;
                case ArtifactKind.GcDump:
                    AnalyzeGCDumpArtifact(artifact, summary, focus);
                    break;
                case ArtifactKind.Trace:
                    AnalyzeTraceArtifact(artifact, summary, focus);
                    break;
                case ArtifactKind.Counters:
                    AnalyzeCountersArtifact(artifact, summary, focus);
                    break;
                default:
                    summary.Findings.Add($"Artifact kind {artifact.Kind} analysis not implemented yet.");
                    break;
            }

            // Add general recommendations
            if (artifact.Status == ArtifactStatus.Completed)
            {
                summary.Recommendations.Add("Artifact collection completed successfully.");
                summary.Recommendations.Add("Use appropriate analysis tools to examine the content.");
            }
            else if (artifact.Status == ArtifactStatus.Failed)
            {
                summary.Recommendations.Add("Artifact collection failed. Check error logs.");
                summary.Recommendations.Add("Retry collection if the issue is transient.");
            }

            return ArtifactAnalysisResult.Success(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze artifact content");
            return ArtifactAnalysisResult.Failure($"Failed to analyze artifact content: {ex.Message}");
        }
    }

    private static void AnalyzeStacksArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Stacks artifact contains managed thread stack traces.");

        try
        {
            if (File.Exists(artifact.FilePath))
            {
                var fileContent = File.ReadAllText(artifact.FilePath);
                var lines = fileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var threadCount = ExtractThreadCount(lines);
                var blockedThreads = CountBlockedThreads(lines);
                var runningThreads = CountRunningThreads(lines);

                summary.KeyMetrics["Thread Count"] = threadCount.ToString();
                summary.KeyMetrics["Blocked Threads"] = blockedThreads.ToString();
                summary.KeyMetrics["Running Threads"] = runningThreads.ToString();

                if (blockedThreads > 0)
                {
                    summary.Findings.Add($"{blockedThreads} threads appear blocked (potential deadlock or contention).");
                }

                if (runningThreads > 5)
                {
                    summary.Findings.Add($"{runningThreads} threads in running state (potential CPU hotspot).");
                }

                if (focus == "threads" || focus == "all")
                {
                    if (blockedThreads > 0)
                    {
                        summary.Recommendations.Add($"Investigate {blockedThreads} blocked threads for circular wait patterns.");
                    }
                    summary.Recommendations.Add("Use `analyze_dump_sos` with 'threads' and 'clrstack' commands for detailed analysis.");
                }

                if (focus == "cpu" || focus == "all" && runningThreads > 0)
                {
                    summary.Recommendations.Add($"Focus on {runningThreads} running threads for CPU hotspots.");
                    summary.Recommendations.Add("Use flame graph visualization to identify hot methods.");
                }
            }
            else
            {
                summary.KeyMetrics["Thread Count"] = "File not found";
            }
        }
        catch (Exception)
        {
            summary.KeyMetrics["Thread Count"] = "Analysis failed";
        }

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Check stack frames for memory allocation patterns.");
        }

        if (focus == "io" || focus == "all")
        {
            summary.Recommendations.Add("Look for I/O operations blocking threads.");
        }
    }

    private static int ExtractThreadCount(string[] lines)
    {
        var threadIds = new HashSet<int>();
        foreach (var line in lines)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"Thread (\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var threadId))
            {
                threadIds.Add(threadId);
            }
        }
        return threadIds.Count;
    }

    private static int CountBlockedThreads(string[] lines)
    {
        return lines.Count(line => line.Contains("Blocked") || line.Contains("Wait") || line.Contains("Lock"));
    }

    private static int CountRunningThreads(string[] lines)
    {
        return lines.Count(line => line.Contains("Running") || line.Contains("Preemptive") || line.Contains("CPU"));
    }

    private static void AnalyzeDumpArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Memory dump contains full process memory snapshot.");
        summary.KeyMetrics["Dump Size"] = FormatBytes(artifact.SizeBytes);

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Use `analyze_dump_sos` with 'dumpheap -stat' to check for large objects.");
            summary.Recommendations.Add("Check GC heap state with 'gcheap' command.");
            summary.Recommendations.Add("Look for memory leaks by examining gen2 and LOH.");
        }

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Use `analyze_dump_sos` with 'threads' and 'clrstack' commands.");
            summary.Recommendations.Add("Examine thread stacks for blocking patterns.");
        }

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Check thread CPU usage and thread pool state.");
        }

        if (focus == "io" || focus == "all")
        {
            summary.Recommendations.Add("Look for I/O operations in thread stacks.");
        }
    }

    private static void AnalyzeGCDumpArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("GC dump contains heap snapshot with object graph.");
        summary.KeyMetrics["Heap Snapshot"] = "Available";

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Use dotnet-gcdump or dotnet-gcdump-analyzer to examine heap.");
            summary.Recommendations.Add("Check for large object arrays and strings.");
            summary.Recommendations.Add("Identify top types by size and count.");
            summary.Recommendations.Add("Examine gen2 and LOH for memory leaks.");
        }

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Check for excessive allocation rates.");
        }

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Examine object references to identify thread-local allocations.");
        }
    }

    private static void AnalyzeTraceArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Trace contains EventPipe events for performance analysis.");
        summary.KeyMetrics["Trace Size"] = FormatBytes(artifact.SizeBytes);

        // .nettrace is a binary format - cannot use ReadAllText
        // This requires TraceEvent library or dotnet-trace convert for proper parsing
        summary.Findings.Add("Binary trace format requires TraceEvent library or dotnet-trace convert for detailed analysis.");
        summary.Recommendations.Add("Use dotnet-trace convert --format Speedscope to convert to a readable format for analysis.");
    }

    private static void AnalyzeCountersArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Counters contain performance metrics over time.");
        summary.KeyMetrics["Metrics"] = "Performance counters";

        try
        {
            if (File.Exists(artifact.FilePath))
            {
                var fileContent = File.ReadAllText(artifact.FilePath);

                var hasCpuMetrics = fileContent.Contains("cpu") || fileContent.Contains("processor") || fileContent.Contains("% Processor Time");
                var hasMemoryMetrics = fileContent.Contains("memory") || fileContent.Contains("gc") || fileContent.Contains("working-set");
                var hasThreadPoolMetrics = fileContent.Contains("threadpool") || fileContent.Contains("worker") || fileContent.Contains("io");
                var hasExceptionMetrics = fileContent.Contains("exception") || fileContent.Contains("error");

                summary.KeyMetrics["CPU Metrics"] = hasCpuMetrics ? "Yes" : "No";
                summary.KeyMetrics["Memory Metrics"] = hasMemoryMetrics ? "Yes" : "No";
                summary.KeyMetrics["ThreadPool Metrics"] = hasThreadPoolMetrics ? "Yes" : "No";
                summary.KeyMetrics["Exception Metrics"] = hasExceptionMetrics ? "Yes" : "No";

                if (hasCpuMetrics)
                {
                    summary.Findings.Add("Counters contain CPU usage metrics for performance analysis.");
                }

                if (hasMemoryMetrics)
                {
                    summary.Findings.Add("Counters contain memory/GC metrics for leak detection.");
                }

                if (hasThreadPoolMetrics)
                {
                    summary.Findings.Add("Counters contain thread pool metrics for starvation analysis.");
                }

                if (hasExceptionMetrics)
                {
                    summary.Findings.Add("Counters contain exception metrics for error analysis.");
                }

                if (focus == "cpu" || focus == "all")
                {
                    if (hasCpuMetrics)
                    {
                        summary.Recommendations.Add("Examine CPU usage and processor time metrics.");
                        summary.Recommendations.Add("Check for CPU spikes and sustained high usage.");
                    }
                    else
                    {
                        summary.Recommendations.Add("Consider adding System.Runtime CPU counters for CPU analysis.");
                    }
                }

                if (focus == "memory" || focus == "all")
                {
                    if (hasMemoryMetrics)
                    {
                        summary.Recommendations.Add("Analyze memory allocation and GC metrics.");
                        summary.Recommendations.Add("Check for memory leaks and high GC pressure.");
                    }
                    else
                    {
                        summary.Recommendations.Add("Consider adding System.Runtime GC counters for memory analysis.");
                    }
                }

                if (focus == "threads" || focus == "all")
                {
                    if (hasThreadPoolMetrics)
                    {
                        summary.Recommendations.Add("Examine thread pool usage and contention metrics.");
                        summary.Recommendations.Add("Check for thread pool starvation or excessive thread creation.");
                    }
                }

                if (focus == "io" || focus == "all")
                {
                    summary.Recommendations.Add("Look for I/O and network-related counters.");
                }

                if (focus == "all")
                {
                    summary.Recommendations.Add("Compare with baseline to identify anomalies.");
                }
            }
            else
            {
                summary.KeyMetrics["Analysis"] = "File not found";
            }
        }
        catch (Exception)
        {
            summary.KeyMetrics["Analysis"] = "Analysis failed";
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    // TODO: Re-enable after fixing MemoryGraph traversal NullReferenceException issues
    // The vendored EventPipeDotNetHeapDumper library has issues constructing MemoryGraph from EventPipe events
    // For now, use .gcdump files instead: mcp1_collect_gcdump + mcp1_visualize_heap_snapshot
    // [McpServerTool(Name = "visualize_nettrace_heap"), Description("Visualize a .nettrace EventPipe heap snapshot. For partial heap data (common), only type_distribution is supported. For full heap graph, all views are supported.")]
    public static async Task<VisualizationResult> VisualizeNettraceHeap(
        string artifactId,
        McpServer server,
        [Description("View kind: 'type_distribution' (default), 'treemap', 'retained_flame'")] string view = "type_distribution",
        [Description("Metric: 'shallow_size' (default), 'count', 'retained_size'")] string metric = "shallow_size",
        [Description("Output format: 'html' (default), 'json'")] string format = "html",
        [Description("Grouping: 'type' (default), 'namespace', 'assembly'")] string groupBy = "type",
        [Description("Maximum types to render")] int maxTypes = 200,
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();
        var preflight = server.Services!.GetRequiredService<NettracePreflight>();

        try
        {
            logger.LogInformation("Step 1: Getting artifact {ArtifactId}", artifactId);
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), cancellationToken);
            if (artifact == null)
            {
                return VisualizationResult.Failure($"Artifact not found: {artifactId}");
            }

            logger.LogInformation("Step 2: Artifact found, Kind: {Kind}", artifact.Kind);
            if (artifact.Kind != ArtifactKind.Trace)
            {
                return VisualizationResult.Failure($"Artifact {artifactId} is not a Trace (.nettrace file).");
            }

            logger.LogInformation("Step 3: Running preflight check");
            var preflightResult = await preflight.CheckAsync(artifact.FilePath, cancellationToken);
            
            if (preflightResult.IsError)
            {
                logger.LogError("Preflight check failed: {Message}", preflightResult.Message);
                return VisualizationResult.Failure($"Preflight check failed: {preflightResult.Message}");
            }

            // No heap data at all
            if (preflightResult.Mode == "no-heap-data")
            {
                logger.LogWarning("Trace contains no heap data: {Message}", preflightResult.Message);
                return VisualizationResult.Failure(
                    $"This .nettrace does not contain GC heap snapshot events. {preflightResult.Message}\n\n" +
                    "For heap-capable .nettrace, use the documented GCHeapSnapshot keyword set:\n" +
                    "  dotnet-trace collect -p <PID> --providers Microsoft-Windows-DotNETRuntime:0x1980001:5 -o heap.nettrace\n\n" +
                    "0x1980001 = GCHeapSnapshot (gc+type+gcheapdump+managedheapcollect+gcheapandtypenames)\n" +
                    "Use hex mask instead of aliases for reliability across dotnet-trace versions.\n\n" +
                    "However, even with correct keywords, .nettrace heap snapshots are often partial " +
                    "and unreliable. For reliable heap visualization, use dotnet-gcdump collect:\n" +
                    "  dotnet-gcdump collect -p <PID> -o heap.gcdump\n\n" +
                    "dotnet-gcdump is the official tool for heap snapshots and triggers special events " +
                    "(GC trigger, sample-profiler flush) to reconstruct the heap graph reliably.");
            }

            // Partial heap data - only type_distribution is supported
            if (preflightResult.Mode == "partial-heap-data")
            {
                logger.LogWarning("Trace contains partial heap data: {Message}", preflightResult.Message);
                
                if (view != "type_distribution")
                {
                    return VisualizationResult.Failure(
                        $"This .nettrace contains partial heap data and does not support '{view}' view.\n\n" +
                        $"{preflightResult.Message}\n\n" +
                        $"Supported views: {string.Join(", ", preflightResult.RecommendedViews)}");
                }

                // For partial data, we can only provide limited type distribution from reader logs
                // Cannot traverse MemoryGraph due to NRE from partial data
                return VisualizationResult.Failure(
                    $"This .nettrace contains partial heap data and cannot be visualized.\n\n" +
                    $"{preflightResult.Message}\n\n" +
                    "Partial heap data (inconsistent BulkNodeEventCount vs NodeIndexLimit) indicates " +
                    "the trace does not contain complete heap snapshot events. The MemoryGraph " +
                    "is built incorrectly and cannot be traversed safely.\n\n" +
                    "For heap visualization, please use .gcdump files instead:\n" +
                    "  - Use mcp1_collect_gcdump to collect heap snapshots\n" +
                    "  - Use mcp1_visualize_heap_snapshot to visualize .gcdump files\n\n" +
                    ".nettrace files can still be used for:\n" +
                    "  - CPU flame graphs (mcp1_visualize_flame_graph)\n" +
                    "  - Performance counters (mcp1_collect_counters)\n" +
                    "  - Trace analysis (mcp1_artifact_summarize)");
            }

            // Full heap graph - should work but currently disabled due to NRE
            logger.LogWarning("Full heap graph detected but currently disabled due to MemoryGraph traversal issues");
            return VisualizationResult.Failure(
                $"This .nettrace contains full heap snapshot data according to preflight ({preflightResult.Message}).\n\n" +
                "However, MemoryGraph traversal still encounters NullReferenceException issues even for full heap data.\n\n" +
                "This appears to be a deeper issue with the vendored EventPipeDotNetHeapDumper library " +
                "or how it constructs MemoryGraph from EventPipe events.\n\n" +
                "For heap visualization, please use .gcdump files instead:\n" +
                "  - Use mcp1_collect_gcdump to collect heap snapshots\n" +
                "  - Use mcp1_visualize_heap_snapshot to visualize .gcdump files");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Nettrace heap visualization failed");
            return VisualizationResult.Failure($"Nettrace heap visualization failed: {ex.Message}");
        }
    }

    [McpServerTool(Name = "visualize_heap_snapshot"), Description("Visualize a .gcdump heap snapshot as type distribution, treemap, retained flame, diff, or retainer paths")]
    public static async Task<VisualizationResult> VisualizeHeapSnapshot(
        string artifactId,
        McpServer server,
        [Description("View kind: 'type_distribution' (default), 'treemap', 'retained_flame', 'diff', 'retainer_paths'")] string view = "type_distribution",
        [Description("Metric: 'shallow_size' (default), 'count', 'retained_size'")] string metric = "shallow_size",
        [Description("Output format: 'html' (default), 'json'")] string format = "html",
        [Description("Grouping: 'type' (default), 'namespace', 'assembly'")] string groupBy = "type",
        [Description("Analysis mode: 'auto' (default), 'reuse' (cache only), 'force' (re-analyze)")] string analysisMode = "auto",
        [Description("Maximum types to render")] int maxTypes = 200,
        [Description("Baseline artifact ID for diff view")] string? baselineArtifactId = null,
        [Description("Target object ID for retainer_paths view")] string? targetObjectId = null,
        [Description("Optional filename to save visualization. If provided, file will be saved and opened in default browser")] string filename = "",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();
        var preparer = server.Services!.GetRequiredService<IHeapSnapshotPreparer>();
        var clrScopeOptions = server.Services!.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClrScopeOptions>>();
        var artifactRoot = clrScopeOptions.Value.GetArtifactRoot();

        // Add 5-minute timeout to prevent hanging (sync operations don't respect cancellation)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var artifact = await artifactStore.GetAsync(new ArtifactId(artifactId), linkedCts.Token);
            if (artifact == null)
            {
                return VisualizationResult.Failure($"Artifact not found: {artifactId}");
            }

            if (artifact.Kind != ArtifactKind.GcDump)
            {
                return VisualizationResult.Failure($"Artifact {artifactId} is not a GcDump.");
            }

            // Parse parameters
            var viewKind = view.ToLowerInvariant() switch
            {
                "treemap" => HeapViewKind.Treemap,
                "retained_flame" => HeapViewKind.RetainedFlame,
                "diff" => HeapViewKind.Diff,
                "retainer_paths" => HeapViewKind.RetainerPaths,
                _ => HeapViewKind.TypeDistribution
            };

            var metricKind = metric.ToLowerInvariant() switch
            {
                "count" => HeapMetricKind.Count,
                "retained_size" => HeapMetricKind.RetainedSize,
                _ => HeapMetricKind.ShallowSize
            };

            var groupByKind = groupBy.ToLowerInvariant() switch
            {
                "namespace" => HeapGroupBy.Namespace,
                "assembly" => HeapGroupBy.Assembly,
                _ => HeapGroupBy.Type
            };

            var analysisModeKind = analysisMode.ToLowerInvariant() switch
            {
                "reuse" => HeapAnalysisMode.Reuse,
                "force" => HeapAnalysisMode.Force,
                _ => HeapAnalysisMode.Auto
            };

            // Handle different view kinds
            string content;
            if (viewKind == HeapViewKind.Diff)
            {
                if (string.IsNullOrEmpty(baselineArtifactId))
                {
                    return VisualizationResult.Failure("baselineArtifactId is required for diff view");
                }

                var baselineArtifact = await artifactStore.GetAsync(new ArtifactId(baselineArtifactId), linkedCts.Token);
                if (baselineArtifact == null)
                {
                    return VisualizationResult.Failure($"Baseline artifact not found: {baselineArtifactId}");
                }

                if (baselineArtifact.Kind != ArtifactKind.GcDump)
                {
                    return VisualizationResult.Failure($"Baseline artifact {baselineArtifactId} is not a GcDump.");
                }

                var baselineOptions = new HeapPreparationOptions
                {
                    Metric = metricKind,
                    AnalysisMode = analysisModeKind,
                    GroupBy = groupByKind,
                    MaxTypes = maxTypes
                };

                var targetOptions = new HeapPreparationOptions
                {
                    Metric = metricKind,
                    AnalysisMode = analysisModeKind,
                    GroupBy = groupByKind,
                    MaxTypes = maxTypes
                };

                var baselinePrepared = await preparer.PrepareAsync(baselineArtifact, baselineOptions, linkedCts.Token);
                var targetPrepared = await preparer.PrepareAsync(artifact, targetOptions, linkedCts.Token);

                var differLogger = server.Services!.GetRequiredService<ILogger<HeapSnapshotDiffer>>();
                var differ = new HeapSnapshotDiffer(differLogger);
                var diff = differ.Diff(baselinePrepared.Snapshot, targetPrepared.Snapshot);
                var diffRenderer = new HeapDiffRenderer();
                content = diffRenderer.RenderHtml(diff, metricKind);
            }
            else if (viewKind == HeapViewKind.RetainerPaths)
            {
                if (string.IsNullOrEmpty(targetObjectId))
                {
                    return VisualizationResult.Failure("targetObjectId is required for retainer_paths view");
                }

                if (!long.TryParse(targetObjectId, out var parsedTargetId))
                {
                    return VisualizationResult.Failure($"targetObjectId '{targetObjectId}' is not a valid numeric ID");
                }

                var graphAdapter = server.Services!.GetRequiredService<IGcDumpGraphAdapter>();
                var retainerBuilder = server.Services!.GetRequiredService<HeapRetainerPathsBuilder>();

                var graph = await graphAdapter.LoadGraphAsync(artifact.FilePath, linkedCts.Token);
                var paths = retainerBuilder.BuildRetainerPaths(graph, targetObjectId);
                var pathsRenderer = new HeapRetainerPathsRenderer();
                content = pathsRenderer.RenderHtml(paths);
            }
            else if (viewKind == HeapViewKind.RetainedFlame)
            {
                var graphAdapter = server.Services!.GetRequiredService<IGcDumpGraphAdapter>();
                var graph = await graphAdapter.LoadGraphAsync(artifact.FilePath, linkedCts.Token);
                var flameRenderer = new HeapRetainedFlameRenderer();
                content = flameRenderer.RenderHtml(graph, metricKind);
            }
            else
            {
                var options = new HeapPreparationOptions
                {
                    Metric = metricKind,
                    AnalysisMode = analysisModeKind,
                    GroupBy = groupByKind,
                    MaxTypes = maxTypes
                };

                logger.LogInformation("Preparing heap snapshot for artifact {ArtifactId}", artifactId);
                var prepared = await preparer.PrepareAsync(artifact, options, linkedCts.Token);

                // Include GcDumpGraphAdapter debug info for debugging
                var debugInfo = new System.Text.StringBuilder();
                debugInfo.AppendLine("<!-- GcDumpGraphAdapter Debug Info:");
                debugInfo.AppendLine($"  NodeIndexLimit={prepared.Snapshot.Metadata.SegmentCount}");
                debugInfo.AppendLine($"  TotalHeapBytes={prepared.Snapshot.Metadata.TotalHeapBytes}");
                debugInfo.AppendLine($"  TotalObjectCount={prepared.Snapshot.Metadata.TotalObjectCount}");
                debugInfo.AppendLine($"  RootCount={prepared.Snapshot.Metadata.RootCount}");
                debugInfo.AppendLine($"  TypeStats Count={prepared.Snapshot.TypeStats?.Count ?? 0}");
                debugInfo.AppendLine("  Check console logs for Step-by-step GcDumpGraphAdapter logging (Step 1-10) -->");

                if (format.ToLowerInvariant() == "json")
                {
                    content = JsonSerializer.Serialize(prepared.Snapshot, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                else if (viewKind == HeapViewKind.Treemap)
                {
                    var renderer = new HeapTreemapRenderer();
                    content = renderer.RenderHtml(prepared.Snapshot, metricKind);
                }
                else
                {
                    var renderer = new HeapTypeDistributionRenderer();
                    content = renderer.RenderHtml(prepared.Snapshot, metricKind);
                }

                // Prepend debug info to HTML content
                if (format.ToLowerInvariant() == "html")
                {
                    content = debugInfo.ToString() + content;
                }
            }

            // Save to file if filename is provided
            if (!string.IsNullOrEmpty(filename))
            {
                await SaveAndOpenVisualizationAsync(content, filename, format.ToLowerInvariant(), logger, artifactRoot, linkedCts.Token);
            }

            return VisualizationResult.Success(content, format);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogError("VisualizeHeapSnapshot timed out after 5 minutes for artifact {ArtifactId}", artifactId);
            return VisualizationResult.Failure("VisualizeHeapSnapshot timed out after 5 minutes - the operation may be hanging");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("VisualizeHeapSnapshot was cancelled for artifact {ArtifactId}", artifactId);
            return VisualizationResult.Failure("VisualizeHeapSnapshot was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Heap snapshot visualization failed for artifact {ArtifactId}", artifactId);
            return VisualizationResult.Failure($"Heap snapshot visualization failed: {ex.Message}");
        }
    }
}

public record ArtifactAnalysisResult(
    bool IsSuccess,
    ArtifactAnalysisSummary? Summary,
    string? Error
)
{
    public static ArtifactAnalysisResult Success(ArtifactAnalysisSummary summary) =>
        new(true, summary, null);

    public static ArtifactAnalysisResult Failure(string error) =>
        new(false, null, error);
}

public record ArtifactAnalysisSummary(
    string ArtifactId,
    string Kind,
    long SizeBytes,
    string Status,
    int Pid,
    DateTime CreatedAt,
    string FilePath,
    Dictionary<string, string> KeyMetrics,
    List<string> Findings,
    List<string> Recommendations
);

public record PatternDetectionResult(
    bool IsSuccess,
    DetectedPattern[] Patterns,
    string? Error
)
{
    public static PatternDetectionResult Success(DetectedPattern[] patterns) =>
        new(true, patterns, null);

    public static PatternDetectionResult Failure(string error) =>
        new(false, Array.Empty<DetectedPattern>(), error);
}

public record DetectedPattern(
    string PatternType,
    string Severity,
    string Description,
    string Recommendation
);

public record VisualizationResult(
    bool IsSuccess,
    string Content,
    string Format,
    string? Error
)
{
    public static VisualizationResult Success(string content, string format) =>
        new(true, content, format, null);

    public static VisualizationResult Failure(string error) =>
        new(false, string.Empty, "none", error);
}

public record StackFrameData
{
    public int ThreadId { get; init; }
    public int FrameIndex { get; init; }
    public string CallSite { get; init; } = string.Empty;
}

public record StackFrame(string ChildSP, string IP, string CallSite);

public record ThreadStack(int ThreadId, string? ThreadName, StackFrame[] Frames);

public record StacksOutput(int Pid, DateTime Timestamp, ThreadStack[] Threads);
