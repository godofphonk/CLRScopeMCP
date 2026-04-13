namespace ClrScope.Mcp.Infrastructure.Utils;

/// <summary>
/// Path security utilities to prevent arbitrary file access
/// </summary>
public static class PathSecurity
{
    /// <summary>
    /// Validate that a file path is within a trusted directory
    /// Prevents bypass via paths like /root/.clrscope_evil/file.txt matching /root/.clrscope
    /// Protects against symlink escape by resolving real paths
    /// Uses case-sensitive comparison on Linux/Unix, case-insensitive on Windows
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <param name="trustedDirectory">Trusted directory (e.g., artifact root)</param>
    /// <returns>True if path is within trusted directory, false otherwise</returns>
    public static bool IsPathWithinDirectory(string filePath, string trustedDirectory)
    {
        try
        {
            // Resolve real paths to prevent symlink escape
            var normalizedFilePath = GetRealPath(filePath);
            var normalizedTrustedDir = GetRealPath(trustedDirectory);

            // Ensure the normalized trusted directory ends with directory separator
            // This prevents /root/.clrscope_evil from matching /root/.clrscope
            if (!normalizedTrustedDir.EndsWith(Path.DirectorySeparatorChar) &&
                !normalizedTrustedDir.EndsWith(Path.AltDirectorySeparatorChar))
            {
                normalizedTrustedDir += Path.DirectorySeparatorChar;
            }

            // Use case-sensitive comparison on Linux/Unix, case-insensitive on Windows
            var comparison = IsCaseSensitiveFileSystem() 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            return normalizedFilePath.StartsWith(normalizedTrustedDir, comparison);
        }
        catch
        {
            // If path resolution fails, treat as invalid
            return false;
        }
    }

    /// <summary>
    /// Get the real path, resolving symlinks to prevent symlink escape attacks
    /// </summary>
    private static string GetRealPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        
        // On Linux/Unix, resolve symlinks using readlink
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            try
            {
                var resolvedPath = fullPath;
                var maxDepth = 10; // Prevent infinite loops with circular symlinks
                
                for (int i = 0; i < maxDepth; i++)
                {
                    if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
                    {
                        break;
                    }

                    var info = new FileInfo(resolvedPath);
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        // This is a symlink, resolve it
                        var target = File.ResolveLinkTarget(resolvedPath, false);
                        if (target != null)
                        {
                            resolvedPath = Path.GetFullPath(target.FullName);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                
                return resolvedPath;
            }
            catch
            {
                // If symlink resolution fails, return the original path
                return fullPath;
            }
        }
        
        return fullPath;
    }

    /// <summary>
    /// Check if the file system is case-sensitive
    /// </summary>
    private static bool IsCaseSensitiveFileSystem()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return false; // Windows is case-insensitive
        }
        
        // Linux/Unix is case-sensitive
        return true;
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
