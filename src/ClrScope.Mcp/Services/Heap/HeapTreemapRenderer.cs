using ClrScope.Mcp.Domain.Heap;
using System.Net;
using System.Text;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Renders type distribution as a simple HTML treemap (nested divs).
/// </summary>
public sealed class HeapTreemapRenderer
{
    public string RenderHtml(HeapSnapshotData snapshot, HeapMetricKind metric)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sb = new StringBuilder();

        sb.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>Heap Treemap</title>
  <style>
    body { font-family: sans-serif; margin: 0; background: #fff; color: #111; }
    .page { padding: 20px; }
    .title { font-size: 24px; font-weight: 700; margin-bottom: 6px; }
    .subtitle { color: #555; margin-bottom: 18px; }
    .treemap-container { border: 1px solid #ddd; border-radius: 8px; overflow: hidden; height: 600px; }
    .treemap { display: flex; flex-wrap: wrap; height: 100%; }
    .treemap-item { border: 1px solid #fff; box-sizing: border-box; padding: 4px; overflow: hidden; cursor: pointer; transition: opacity 0.2s; }
    .treemap-item:hover { opacity: 0.8; }
    .treemap-name { font-size: 11px; font-weight: 600; margin-bottom: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .treemap-size { font-size: 10px; color: #555; }
    .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
  </style>
</head>
<body>
  <div class="page">
""");

        sb.AppendLine($"""<div class="title">Heap Treemap</div>""");
        sb.AppendLine($"""<div class="subtitle">Artifact: <span class="mono">{snapshot.Artifact.ArtifactId.Value}</span> | Total heap: {FormatBytes(snapshot.Metadata.TotalHeapBytes)}</div>""");

        AppendTreemap(sb, snapshot.TypeStats, metric, snapshot.Metadata.TotalHeapBytes);

        sb.Append("""
  </div>
</body>
</html>
""");

        return sb.ToString();
    }

    private static void AppendTreemap(
        StringBuilder sb,
        IReadOnlyList<TypeStatData> typeStats,
        HeapMetricKind metric,
        long totalBytes)
    {
        sb.AppendLine("""<div class="treemap-container"><div class="treemap">""");

        var totalValue = typeStats.Sum(t => GetValue(t, metric));

        foreach (var stat in typeStats)
        {
            var value = GetValue(stat, metric);
            var percentage = totalValue > 0 ? (value * 100.0 / totalValue) : 0;
            var color = GetColor(stat.TypeName);

            sb.AppendLine($$"""
<div class="treemap-item" style="flex: {percentage}; background: {color};" title="{Html(stat.TypeName)}&#10;{FormatBytes(stat.ShallowSizeBytes)}&#10;Count: {stat.Count}">
  <div class="treemap-name mono">{Html(ShortName(stat.TypeName))}</div>
  <div class="treemap-size">{FormatBytes(stat.ShallowSizeBytes)} ({percentage:0.##}%)</div>
</div>
""");
        }

        sb.AppendLine("""</div></div>""");
    }

    private static long GetValue(TypeStatData stat, HeapMetricKind metric) =>
        metric switch
        {
            HeapMetricKind.Count => stat.Count,
            _ => stat.ShallowSizeBytes
        };

    private static string GetColor(string typeName)
    {
        // Simple hash-based color generation
        var hash = typeName.GetHashCode();
        var hue = Math.Abs(hash % 360);
        return $"hsl({hue}, 70%, 85%)";
    }

    private static string ShortName(string typeName)
    {
        var lastDot = typeName.LastIndexOf('.');
        return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }
}
