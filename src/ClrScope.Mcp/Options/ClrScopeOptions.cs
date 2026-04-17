namespace ClrScope.Mcp.Options;

public class ClrScopeOptions
{
    public const string SectionName = "ClrScope";

    public string ArtifactRoot { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
    public string[] DefaultCountersProviders { get; set; } = new[] { "System.Runtime" };

    public string GetArtifactRoot()
    {
        if (!string.IsNullOrEmpty(ArtifactRoot))
        {
            return Path.GetFullPath(ArtifactRoot);
        }

        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clrscope");
        Directory.CreateDirectory(defaultPath);
        return Path.GetFullPath(defaultPath);
    }

    public string GetDatabasePath()
    {
        if (!string.IsNullOrEmpty(DatabasePath))
        {
            return Path.GetFullPath(DatabasePath);
        }

        var artifactRoot = GetArtifactRoot();
        return Path.GetFullPath(Path.Combine(artifactRoot, "clrscope.db"));
    }
}
