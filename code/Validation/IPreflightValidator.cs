using ClrScope.Mcp.Contracts;

namespace ClrScope.Mcp.Validation;

public interface IPreflightValidator
{
    Task<PreflightResult> ValidateCollectAsync(int pid, CancellationToken cancellationToken = default);
}
