namespace ClrScope.Mcp.Contracts;

public record Result<T>(bool IsSuccess, T? Value, ClrScopeError? Error, string? ErrorMessage)
{
    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(ClrScopeError error, string? message = null) => new(false, default, error, message);
}
