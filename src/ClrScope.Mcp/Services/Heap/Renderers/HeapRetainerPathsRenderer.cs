using ClrScope.Mcp.Domain.Heap.Data;

namespace ClrScope.Mcp.Services.Heap;

/// <summary>
/// Renderer for retainer paths visualization.
/// </summary>
public sealed class HeapRetainerPathsRenderer
{
    public string RenderHtml(RetainerPathData paths)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='utf-8'>");
        sb.AppendLine("    <title>Retainer Paths</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("        .header { background: #f5f5f5; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
        sb.AppendLine("        .chain { background: #fff; border: 1px solid #ddd; padding: 15px; margin-bottom: 15px; border-radius: 5px; }");
        sb.AppendLine("        .chain-header { font-weight: bold; margin-bottom: 10px; }");
        sb.AppendLine("        .step { padding: 8px; margin: 5px 0; border-left: 3px solid #4CAF50; padding-left: 10px; }");
        sb.AppendLine("        .step-root { border-left-color: #FF9800; }");
        sb.AppendLine("        .step-type { font-weight: bold; }");
        sb.AppendLine("        .step-namespace { color: #666; font-size: 0.9em; }");
        sb.AppendLine("        .step-field { color: #2196F3; }");
        sb.AppendLine("        .step-size { color: #666; font-size: 0.9em; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <h1>Retainer Paths</h1>");
        sb.AppendLine("    <div class='header'>");
        sb.AppendLine($"        <p><strong>Target Object:</strong> {paths.TargetObjectId}</p>");
        sb.AppendLine($"        <p><strong>Target Type:</strong> {paths.TargetTypeName}</p>");
        sb.AppendLine($"        <p><strong>Retainer Chains:</strong> {paths.RetainerChains.Count}</p>");
        sb.AppendLine("    </div>");

        if (paths.RetainerChains.Count == 0)
        {
            sb.AppendLine("    <p>No retainer paths found.</p>");
        }
        else
        {
            for (int i = 0; i < paths.RetainerChains.Count; i++)
            {
                var chain = paths.RetainerChains[i];
                sb.AppendLine($"    <div class='chain'>");
                sb.AppendLine($"        <div class='chain-header'>Chain {i + 1} - Retained Size: {FormatBytes(chain.RetainedSizeBytes)}</div>");

                foreach (var step in chain.Steps)
                {
                    var rootClass = step.IsRoot ? "step-root" : "";
                    sb.AppendLine($"        <div class='step {rootClass}'>");
                    sb.AppendLine($"            <div class='step-type'>{step.TypeName}</div>");
                    sb.AppendLine($"            <div class='step-namespace'>{step.Namespace}</div>");
                    if (!string.IsNullOrEmpty(step.FieldName))
                    {
                        sb.AppendLine($"            <div class='step-field'>Field: {step.FieldName}</div>");
                    }
                    sb.AppendLine($"            <div class='step-size'>Shallow Size: {FormatBytes(step.ShallowSizeBytes)}</div>");
                    sb.AppendLine($"        </div>");
                }

                sb.AppendLine("    </div>");
            }
        }

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
