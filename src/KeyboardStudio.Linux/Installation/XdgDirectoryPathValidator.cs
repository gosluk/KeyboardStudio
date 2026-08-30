namespace KeyboardStudio.Linux;

public static class XdgDirectoryPathValidator
{
    public static bool IsSafe(XdgDirectoryPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return IsSafeUserChild(paths.UserXkbRoot, paths.ConfigHome) &&
               IsSafeUserChild(paths.KeyboardStudioStateRoot, paths.StateHome);
    }

    private static bool IsSafeUserChild(string path, string expectedParent)
    {
        if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(expectedParent))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var fullParent = Path.GetFullPath(expectedParent);
        if (IsSystemPath(fullPath) || IsSystemPath(fullParent))
        {
            return false;
        }

        var relative = Path.GetRelativePath(fullParent, fullPath);
        return relative is not "." and not ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }

    private static bool IsSystemPath(string path) =>
        string.Equals(path, "/usr", StringComparison.Ordinal) ||
        path.StartsWith("/usr/", StringComparison.Ordinal) ||
        string.Equals(path, "/etc", StringComparison.Ordinal) ||
        path.StartsWith("/etc/", StringComparison.Ordinal);
}
