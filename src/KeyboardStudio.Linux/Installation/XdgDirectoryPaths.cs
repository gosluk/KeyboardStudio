namespace KeyboardStudio.Linux;

public sealed record XdgDirectoryPaths(
    string ConfigHome,
    string StateHome,
    string UserXkbRoot,
    string KeyboardStudioStateRoot);
