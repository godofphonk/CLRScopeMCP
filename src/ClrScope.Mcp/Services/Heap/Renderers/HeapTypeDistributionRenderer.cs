using ClrScope.Mcp.Domain.Heap.Data;
using ClrScope.Mcp.Domain.Heap.Enums;
using System.Net;
using System.Text;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Renders type distribution as HTML table.
/// </summary>
public sealed class HeapTypeDistributionRenderer
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
  <title>Heap Type Distribution</title>
  <style>
    body { font-family: sans-serif; margin: 0; background: #fff; color: #111; }
    .page { padding: 20px; }
    .title { font-size: 24px; font-weight: 700; margin-bottom: 6px; }
    .subtitle { color: #555; margin-bottom: 18px; }
    table { width: 100%; border-collapse: collapse; font-size: 13px; }
    th, td { border-bottom: 1px solid #eee; padding: 8px 10px; text-align: left; vertical-align: top; }
    th { background: #f8f8f8; font-weight: 700; }
    tr:hover td { background: #fcfcfc; }
    .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
    .right { text-align: right; }
    .muted { color: #666; }
  </style>
</head>
<body>
  <div class="page">
""");

        sb.AppendLine($"""<div class="title">Heap Type Distribution</div>""");
        sb.AppendLine($"""<div class="subtitle">Artifact: <span class="mono">{snapshot.Artifact.ArtifactId.Value}</span> | Total heap: {FormatBytes(snapshot.Metadata.TotalHeapBytes)} | Total objects: {FormatNumber(snapshot.Metadata.TotalObjectCount)}</div>""");

        AppendTypeTable(sb, snapshot.TypeStats, metric);

        sb.Append("""
  </div>
</body>
</html>
""");

        return sb.ToString();
    }

    private static void AppendTypeTable(StringBuilder sb, IReadOnlyList<TypeStatData> typeStats, HeapMetricKind metric)
    {
        sb.AppendLine("""<table>""");
        sb.AppendLine("""<thead>""");
        sb.AppendLine("""<tr>""");
        sb.AppendLine("""<th>Type</th>""");
        sb.AppendLine("""<th>Namespace</th>""");
        sb.AppendLine("""<th class="right">Count</th>""");
        sb.AppendLine("""<th class="right">Shallow Size</th>""");
        sb.AppendLine("""<th class="right">% of Total</th>""");
        sb.AppendLine("""</tr>""");
        sb.AppendLine("""</thead>""");
        sb.AppendLine("""<tbody>""");

        var totalBytes = typeStats.Sum(t => t.ShallowSizeBytes);

        foreach (var stat in typeStats)
        {
            var percent = totalBytes > 0 ? (stat.ShallowSizeBytes * 100.0 / totalBytes) : 0;

            sb.AppendLine($"""
<tr>
  <td><span class="mono">{Html(stat.TypeName)}</span></td>
  <td class="muted">{Html(stat.Namespace)}</td>
  <td class="right">{FormatNumber(stat.Count)}</td>
  <td class="right">{FormatBytes(stat.ShallowSizeBytes)}</td>
  <td class="right">{percent:0.##}%</td>
</tr>
""");
        }

        sb.AppendLine("""</tbody>""");
        sb.AppendLine("""</table>""");
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);
    private static string FormatNumber(long value) => value.ToString("N0");

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
