using ClrScope.Mcp.Contracts;

namespace ClrScope.Mcp.Contracts;

public record PreflightResult(
    bool IsValid,
    ClrScopeError? Error,
    string? Message
)
{
    public static PreflightResult Success() => new(true, null, null);
    public static PreflightResult Failure(ClrScopeError error, string message) => new(false, error, message);
}
