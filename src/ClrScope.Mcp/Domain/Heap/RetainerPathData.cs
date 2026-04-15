namespace ClrScope.Mcp.Domain.Heap;

/// <summary>
/// Retainer path data for showing object retention chains.
/// </summary>
public sealed class RetainerPathData
{
    public required string TargetObjectId { get; set; }
    public required string TargetTypeName { get; set; }
    public required List<RetainerChainData> RetainerChains { get; set; }
}

/// <summary>
/// Single retainer chain from root to target object.
/// </summary>
public sealed class RetainerChainData
{
    public required List<RetainerStepData> Steps { get; set; }
    public required long RetainedSizeBytes { get; set; }
}

/// <summary>
/// Single step in retainer chain.
/// </summary>
public sealed class RetainerStepData
{
    public required string ObjectId { get; set; }
    public required string TypeName { get; set; }
    public required string Namespace { get; set; }
    public required string AssemblyName { get; set; }
    public required string FieldName { get; set; }
    public required long ShallowSizeBytes { get; set; }
    public required bool IsRoot { get; set; }
}
