namespace ClrScope.Mcp.Domain.Sessions;

public record SessionId(string Value)
{
    public static SessionId New() => new($"ses_{Guid.NewGuid():N}");
}
