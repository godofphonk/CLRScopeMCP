using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Infrastructure.Utils;
using ClrScope.Mcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ClrScope.Mcp.Tools.Analysis;

[McpServerToolType]
public sealed class SummaryTools
{
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

            // Only dump and gcdump artifacts support pattern detection
            if (artifact.Kind != ArtifactKind.Dump && artifact.Kind != ArtifactKind.GcDump)
            {
                return PatternDetectionResult.Failure($"Pattern detection only supports Dump and GcDump artifacts, got: {artifact.Kind}");
            }

            // Check if dotnet-dump is available
            if (!await sosAnalyzer.IsAvailableAsync(cancellationToken))
            {
                return PatternDetectionResult.Failure("dotnet-dump CLI not found. Pattern detection requires SOS commands.");
            }

            var detectedPatterns = new List<DetectedPattern>();
            var patternsToDetect = patternTypes.ToLowerInvariant() == "all"
                ? new[] { "memory_leaks", "deadlocks", "thread_pool", "high_cpu", "excessive_allocations" }
                : patternTypes.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Detect patterns based on artifact kind
            if (artifact.Kind == ArtifactKind.Dump)
            {
                await DetectDumpPatterns(artifact.FilePath, sosAnalyzer, patternsToDetect, detectedPatterns, cancellationToken);
            }
            else if (artifact.Kind == ArtifactKind.GcDump)
            {
                await DetectGCDumpPatterns(artifact.FilePath, sosAnalyzer, patternsToDetect, detectedPatterns, cancellationToken);
            }

            return PatternDetectionResult.Success(detectedPatterns.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pattern detection failed for artifact {ArtifactId}", artifactId);
            return PatternDetectionResult.Failure($"Pattern detection failed: {ex.Message}");
        }
    }

    [McpServerTool(Name = "visualize_flame_graph"), Description("Generate flame graph visualization from stack traces")]
    public static async Task<VisualizationResult> VisualizeFlameGraph(
        string artifactId,
        McpServer server,
        [Description("Visualization format: 'svg' (default), 'html'")] string format = "svg",
        CancellationToken cancellationToken = default)
    {
        var artifactStore = server.Services!.GetRequiredService<ISqliteArtifactStore>();
        var logger = server.Services!.GetRequiredService<ILogger<SummaryTools>>();
        var options = server.Services!.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClrScopeOptions>>();

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

            // Only Stacks and Dump artifacts support flame graphs
            if (artifact.Kind != ArtifactKind.Stacks && artifact.Kind != ArtifactKind.Dump)
            {
                return VisualizationResult.Failure($"Flame graph visualization only supports Stacks and Dump artifacts, got: {artifact.Kind}");
            }

            // Generate flame graph
            var flameGraph = GenerateFlameGraph(artifact, format.ToLowerInvariant());

            return VisualizationResult.Success(flameGraph, format.ToLowerInvariant());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flame graph visualization failed for artifact {ArtifactId}", artifactId);
            return VisualizationResult.Failure($"Flame graph visualization failed: {ex.Message}");
        }
    }

    private static string GenerateFlameGraph(Artifact artifact, string format)
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
                    // Parse JSON format
                    stackFrames = System.Text.Json.JsonSerializer.Deserialize<List<StackFrameData>>(fileContent);
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
            return GenerateSvgFlameGraph(stackFrames, artifact);
        }
        else
        {
            return GenerateHtmlFlameGraph(stackFrames, artifact);
        }
    }

    private static string GeneratePlaceholderFlameGraph(Artifact artifact, string format)
    {
        var flameGraphData = new System.Text.StringBuilder();

        if (format == "svg")
        {
            flameGraphData.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            flameGraphData.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"600\">");
            flameGraphData.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#f0f0f0\"/>");
            flameGraphData.AppendLine("  <text x=\"10\" y=\"30\" font-family=\"Arial\" font-size=\"14\" fill=\"#333\">");
            flameGraphData.AppendLine($"    Flame Graph for {artifact.ArtifactId.Value} ({artifact.Kind})");
            flameGraphData.AppendLine("  </text>");
            flameGraphData.AppendLine("  <text x=\"10\" y=\"50\" font-family=\"Arial\" font-size=\"12\" fill=\"#666\">");
            flameGraphData.AppendLine("    No stack data available or could not be parsed");
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
            flameGraphData.AppendLine("  </style>");
            flameGraphData.AppendLine("</head>");
            flameGraphData.AppendLine("<body>");
            flameGraphData.AppendLine("  <h1>Flame Graph for " + artifact.ArtifactId.Value + " (" + artifact.Kind + ")</h1>");
            flameGraphData.AppendLine("  <p>No stack data available or could not be parsed</p>");
            flameGraphData.AppendLine("</body>");
            flameGraphData.AppendLine("</html>");
        }

        return flameGraphData.ToString();
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

    private static string GenerateSvgFlameGraph(List<StackFrameData> frames, Artifact artifact)
    {
        var svg = new System.Text.StringBuilder();
        var width = 1200;
        var height = Math.Min(600, frames.Count * 25 + 100);
        var colors = new[] { "#ff6b6b", "#4ecdc4", "#45b7d1", "#96ceb4", "#ffeaa7", "#dfe6e9", "#fd79a8", "#a29bfe" };

        svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"" + width + "\" height=\"" + height + "\">");
        svg.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#f8f9fa\"/>");
        svg.AppendLine("  <text x=\"10\" y=\"30\" font-family=\"Arial\" font-size=\"14\" fill=\"#2d3436\">");
        svg.AppendLine("    Flame Graph for " + artifact.ArtifactId.Value + " (" + frames.Count + " frames)");
        svg.AppendLine("  </text>");

        // Group frames by thread
        var threadGroups = frames.GroupBy(f => f.ThreadId).ToList();
        var y = 50;
        var frameHeight = 20;

        foreach (var threadGroup in threadGroups)
        {
            var threadFrames = threadGroup.ToList();
            var x = 10;
            var frameWidth = (width - 20.0) / threadFrames.Count;

            foreach (var frame in threadFrames)
            {
                var color = colors[frame.ThreadId % colors.Length];
                var displayText = TruncateCallSite(frame.CallSite, (int)(frameWidth / 6));

                svg.AppendLine("  <rect x=\"" + x + "\" y=\"" + y + "\" width=\"" + (frameWidth - 1) + "\" height=\"" + frameHeight + "\" fill=\"" + color + "\" rx=\"2\"/>");
                svg.AppendLine("  <text x=\"" + (x + 5) + "\" y=\"" + (y + 14) + "\" font-family=\"Arial\" font-size=\"10\" fill=\"white\">" + displayText + "</text>");

                x += (int)frameWidth;
            }

            y += frameHeight + 5;
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static string GenerateHtmlFlameGraph(List<StackFrameData> frames, Artifact artifact)
    {
        var html = new System.Text.StringBuilder();
        var colors = new[] { "#ff6b6b", "#4ecdc4", "#45b7d1", "#96ceb4", "#ffeaa7", "#dfe6e9", "#fd79a8", "#a29bfe" };

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("  <title>Flame Graph - " + artifact.ArtifactId.Value + "</title>");
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

    private static string TruncateCallSite(string callSite, int maxLength)
    {
        if (string.IsNullOrEmpty(callSite) || callSite.Length <= maxLength)
            return callSite;
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
            Recommendation: "Use dotnet-gcdump-analyze to examine allocation rates and patterns"
        ));
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
        summary.KeyMetrics["Thread Count"] = "Unknown (analyze file for details)";

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Use `analyze_dump_sos` with 'threads' and 'clrstack' commands to analyze.");
            summary.Recommendations.Add("Look for threads blocked on locks, monitors, or synchronization primitives.");
        }

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Check stack frames for memory allocation patterns.");
        }

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Identify hot methods appearing frequently across thread stacks.");
        }

        if (focus == "io" || focus == "all")
        {
            summary.Recommendations.Add("Look for I/O operations blocking threads.");
        }
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

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Use PerfView or dotnet-trace analyze to examine CPU samples.");
            summary.Recommendations.Add("Look for hot methods with high CPU time.");
        }

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Check for allocation patterns and GC activity.");
            summary.Recommendations.Add("Examine GC heap allocation rate and pause times.");
        }

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Analyze thread pool usage and contention.");
        }

        if (focus == "io" || focus == "all")
        {
            summary.Recommendations.Add("Look for I/O operations and network activity.");
        }
    }

    private static void AnalyzeCountersArtifact(Artifact artifact, ArtifactAnalysisSummary summary, string focus)
    {
        summary.Findings.Add("Counters contain performance metrics over time.");
        summary.KeyMetrics["Metrics"] = "Performance counters";

        if (focus == "cpu" || focus == "all")
        {
            summary.Recommendations.Add("Examine CPU usage and processor time metrics.");
            summary.Recommendations.Add("Check for CPU spikes and sustained high usage.");
        }

        if (focus == "memory" || focus == "all")
        {
            summary.Recommendations.Add("Analyze memory allocation and GC metrics.");
            summary.Recommendations.Add("Check for memory leaks and high GC pressure.");
        }

        if (focus == "threads" || focus == "all")
        {
            summary.Recommendations.Add("Examine thread pool usage and contention metrics.");
            summary.Recommendations.Add("Check for thread pool starvation or excessive thread creation.");
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
