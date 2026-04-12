namespace ClrScope.Mcp.Domain;

public record ArtifactId(string Value)
{
    public static ArtifactId New() => new($"art_{Guid.NewGuid():N}");
}
