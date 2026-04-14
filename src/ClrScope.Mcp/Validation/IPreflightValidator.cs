using ClrScope.Mcp.Contracts;

namespace ClrScope.Mcp.Validation;

public enum CollectionOperationType
{
    Dump,
    Trace,
    Counters,
    Stacks,
    GcDump
}

public interface IPreflightValidator
{
    Task<PreflightResult> ValidateCollectAsync(int pid, CollectionOperationType operationType, CancellationToken cancellationToken = default);
}
