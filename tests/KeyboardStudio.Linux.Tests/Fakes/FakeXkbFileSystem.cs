using System.Text;
using KeyboardStudio.Linux;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// An in-memory filesystem. Adding a file implies its containing directories exist, which matches
/// how the real thing behaves and keeps test setup to the paths a test actually cares about.
/// </summary>
public sealed class FakeXkbFileSystem : IXkbFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

    public FakeXkbFileSystem AddDirectory(string path)
    {
        for (var current = Normalize(path); current is not null; current = Parent(current))
        {
            _directories.Add(current);
        }

        return this;
    }

    public FakeXkbFileSystem AddFile(string path, string content)
    {
        var normalized = Normalize(path);
        _files[normalized] = content;

        var parent = Parent(normalized);
        if (parent is not null)
        {
            AddDirectory(parent);
        }

        return this;
    }

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public IReadOnlyList<string> EnumerateFiles(string path)
    {
        var directory = Normalize(path);
        return [.. _files.Keys.Where(file => Parent(file) == directory).Order(StringComparer.Ordinal)];
    }

    public Stream OpenRead(string path)
    {
        var normalized = Normalize(path);
        return _files.TryGetValue(normalized, out var content)
            ? new MemoryStream(Encoding.UTF8.GetBytes(content))
            : throw new FileNotFoundException($"No fake file at '{normalized}'.", normalized);
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(path);

    private static string? Parent(string path)
    {
        var parent = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(parent) ? null : Normalize(parent);
    }
}
