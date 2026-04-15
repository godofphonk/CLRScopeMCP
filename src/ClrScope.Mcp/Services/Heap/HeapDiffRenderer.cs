using ClrScope.Mcp.Domain.Heap;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Renderer for heap snapshot diff visualization.
/// </summary>
public sealed class HeapDiffRenderer
{
    public string RenderHtml(HeapSnapshotDiffData diff, HeapMetricKind metricKind)
    {
        var metric = metricKind == HeapMetricKind.Count ? "count" : "shallow_size";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='utf-8'>");
        sb.AppendLine("    <title>Heap Snapshot Diff</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("        .header { background: #f5f5f5; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
        sb.AppendLine("        table { border-collapse: collapse; width: 100%; }");
        sb.AppendLine("        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
        sb.AppendLine("        th { background: #4CAF50; color: white; }");
        sb.AppendLine("        .status-added { color: green; font-weight: bold; }");
        sb.AppendLine("        .status-removed { color: red; font-weight: bold; }");
        sb.AppendLine("        .status-increased { color: orange; font-weight: bold; }");
        sb.AppendLine("        .status-decreased { color: blue; font-weight: bold; }");
        sb.AppendLine("        .delta-positive { color: red; }");
        sb.AppendLine("        .delta-negative { color: green; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <h1>Heap Snapshot Diff</h1>");
        sb.AppendLine("    <div class='header'>");
        sb.AppendLine($"        <p><strong>Baseline:</strong> {diff.Baseline.Artifact.ArtifactId.Value}</p>");
        sb.AppendLine($"        <p><strong>Target:</strong> {diff.Target.Artifact.ArtifactId.Value}</p>");
        sb.AppendLine($"        <p><strong>Metric:</strong> {metric}</p>");
        sb.AppendLine($"        <p><strong>Types Changed:</strong> {diff.TypeDiffs.Count}</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <table>");
        sb.AppendLine("        <tr>");
        sb.AppendLine("            <th>Status</th>");
        sb.AppendLine("            <th>Type</th>");
        sb.AppendLine("            <th>Namespace</th>");
        sb.AppendLine("            <th>Baseline Count</th>");
        sb.AppendLine("            <th>Target Count</th>");
        sb.AppendLine("            <th>Count Delta</th>");
        sb.AppendLine("            <th>Baseline Size</th>");
        sb.AppendLine("            <th>Target Size</th>");
        sb.AppendLine("            <th>Size Delta</th>");
        sb.AppendLine("            <th>% Change</th>");
        sb.AppendLine("        </tr>");

        foreach (var typeDiff in diff.TypeDiffs)
        {
            var statusClass = $"status-{typeDiff.Status.ToString().ToLower()}";
            var deltaClass = typeDiff.ShallowSizeDelta > 0 ? "delta-positive" : "delta-negative";
            var deltaSign = typeDiff.ShallowSizeDelta > 0 ? "+" : "";
            var percentSign = typeDiff.ShallowSizePercentChange > 0 ? "+" : "";
            var percentValue = double.IsInfinity(typeDiff.ShallowSizePercentChange)
                ? "N/A"
                : $"{percentSign}{typeDiff.ShallowSizePercentChange:F2}%";

            sb.AppendLine("        <tr>");
            sb.AppendLine($"            <td class='{statusClass}'>{typeDiff.Status}</td>");
            sb.AppendLine($"            <td>{typeDiff.TypeName}</td>");
            sb.AppendLine($"            <td>{typeDiff.Namespace}</td>");
            sb.AppendLine($"            <td>{typeDiff.BaselineCount:N0}</td>");
            sb.AppendLine($"            <td>{typeDiff.TargetCount:N0}</td>");
            sb.AppendLine($"            <td class='{deltaClass}'>{deltaSign}{typeDiff.CountDelta:N0}</td>");
            sb.AppendLine($"            <td>{FormatBytes(typeDiff.BaselineShallowSize)}</td>");
            sb.AppendLine($"            <td>{FormatBytes(typeDiff.TargetShallowSize)}</td>");
            sb.AppendLine($"            <td class='{deltaClass}'>{deltaSign}{FormatBytes(typeDiff.ShallowSizeDelta)}</td>");
            sb.AppendLine($"            <td>{percentValue}</td>");
            sb.AppendLine("        </tr>");
        }

        sb.AppendLine("    </table>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string FormatBytes(long bytes)
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
