namespace ClrScope.Mcp.Options;

public class ClrScopeOptions
{
    public const string SectionName = "ClrScope";

    public string ArtifactRoot { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;

    public string GetArtifactRoot()
    {
        if (!string.IsNullOrEmpty(ArtifactRoot))
        {
            return ArtifactRoot;
        }

        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clrscope");
        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }

    public string GetDatabasePath()
    {
        if (!string.IsNullOrEmpty(DatabasePath))
        {
            return DatabasePath;
        }

        var artifactRoot = GetArtifactRoot();
        return Path.Combine(artifactRoot, "clrscope.db");
    }
}
