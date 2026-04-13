namespace ClrScope.Mcp.Domain.Artifacts;

public record ArtifactId(string Value)
{
    public static ArtifactId New() => new($"art_{Guid.NewGuid():N}");
}
