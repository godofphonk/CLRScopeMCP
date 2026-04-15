using ClrScope.Mcp.Domain.Heap;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Renderer for heap retained flame graph.
/// </summary>
public sealed class HeapRetainedFlameRenderer
{
    public string RenderHtml(HeapGraphData graph, HeapMetricKind metricKind)
    {
        var metric = metricKind == HeapMetricKind.Count ? "count" : "retained_size";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='utf-8'>");
        sb.AppendLine("    <title>Heap Retained Flame Graph</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        .flame-container {");
        sb.AppendLine("            width: 100%;");
        sb.AppendLine("            height: 600px;");
        sb.AppendLine("            overflow: auto;");
        sb.AppendLine("            font-family: monospace;");
        sb.AppendLine("        }");
        sb.AppendLine("        .flame-rect {");
        sb.AppendLine("            position: absolute;");
        sb.AppendLine("            border: 1px solid #fff;");
        sb.AppendLine("            overflow: hidden;");
        sb.AppendLine("        }");
        sb.AppendLine("        .flame-rect:hover {");
        sb.AppendLine("            opacity: 0.8;");
        sb.AppendLine("        }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <h1>Heap Retained Flame Graph</h1>");
        sb.AppendLine($"    <p>Metric: {metric}</p>");
        sb.AppendLine("    <div class='flame-container' id='flame-container'></div>");
        sb.AppendLine("    <script>");
        sb.AppendLine("        // Simplified flame graph rendering");
        sb.AppendLine("        // Full implementation would use D3.js or similar library");
        sb.AppendLine("        const container = document.getElementById('flame-container');");
        sb.AppendLine("        container.innerHTML = '<p>Flame graph rendering requires full graph data.</p>';");
        sb.AppendLine("    </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
