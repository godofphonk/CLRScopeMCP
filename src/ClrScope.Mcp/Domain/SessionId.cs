namespace ClrScope.Mcp.Domain;

public record SessionId(string Value)
{
    public static SessionId New() => new($"ses_{Guid.NewGuid():N}");
}
