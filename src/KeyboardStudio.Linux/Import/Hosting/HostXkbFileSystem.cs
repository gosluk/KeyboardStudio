namespace KeyboardStudio.Linux;

/// <summary>Reads the real filesystem.</summary>
public sealed class HostXkbFileSystem : IXkbFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public IReadOnlyList<string> EnumerateFiles(string path) =>
        Directory.Exists(path) ? Directory.GetFiles(path) : [];

    public Stream OpenRead(string path) => File.OpenRead(path);
}
