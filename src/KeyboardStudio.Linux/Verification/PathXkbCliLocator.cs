namespace KeyboardStudio.Linux;

public sealed class PathXkbCliLocator : IXkbCliLocator
{
    public string? Find()
    {
        var executableName = OperatingSystem.IsWindows() ? "xkbcli.exe" : "xkbcli";
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
