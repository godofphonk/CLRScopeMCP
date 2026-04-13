namespace ClrScope.Mcp.Infrastructure.Utils;

/// <summary>
/// Path security utilities to prevent arbitrary file access
/// </summary>
public static class PathSecurity
{
    /// <summary>
    /// Validate that a file path is within a trusted directory
    /// Prevents bypass via paths like /root/.clrscope_evil/file.txt matching /root/.clrscope
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <param name="trustedDirectory">Trusted directory (e.g., artifact root)</param>
    /// <returns>True if path is within trusted directory, false otherwise</returns>
    public static bool IsPathWithinDirectory(string filePath, string trustedDirectory)
    {
        try
        {
            // Normalize both paths
            var normalizedFilePath = Path.GetFullPath(filePath);
            var normalizedTrustedDir = Path.GetFullPath(trustedDirectory);

            // Ensure the normalized trusted directory ends with directory separator
            // This prevents /root/.clrscope_evil from matching /root/.clrscope
            if (!normalizedTrustedDir.EndsWith(Path.DirectorySeparatorChar) &&
                !normalizedTrustedDir.EndsWith(Path.AltDirectorySeparatorChar))
            {
                normalizedTrustedDir += Path.DirectorySeparatorChar;
            }

            // Check if the file path starts with the trusted directory with separator
            return normalizedFilePath.StartsWith(normalizedTrustedDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If path resolution fails, treat as invalid
            return false;
        }
    }

    /// <summary>
    /// Validate and throw if path is not within trusted directory
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <param name="trustedDirectory">Trusted directory</param>
    /// <exception cref="UnauthorizedAccessException">Thrown if path is outside trusted directory</exception>
    public static void EnsurePathWithinDirectory(string filePath, string trustedDirectory)
    {
        if (!IsPathWithinDirectory(filePath, trustedDirectory))
        {
            throw new UnauthorizedAccessException(
                $"Path '{filePath}' is outside trusted directory '{trustedDirectory}'");
        }
    }
}
