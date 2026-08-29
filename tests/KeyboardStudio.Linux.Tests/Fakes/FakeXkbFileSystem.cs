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
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);

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

    /// <summary>
    /// Adds a file that exists but cannot be opened, as one owned by another user is. Readers are
    /// expected to treat it as they treat a missing one, and the two cases only look alike from
    /// the outside if a test can produce both.
    /// </summary>
    public FakeXkbFileSystem AddUnreadableFile(string path)
    {
        AddFile(path, string.Empty);
        _unreadable.Add(Normalize(path));
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
        if (_unreadable.Contains(normalized))
        {
            throw new UnauthorizedAccessException($"Access to '{normalized}' is denied.");
        }

        return _files.TryGetValue(normalized, out var content)
            ? new MemoryStream(Encoding.UTF8.GetBytes(content))
            : throw new FileNotFoundException($"No fake file at '{normalized}'.", normalized);
    }

    /// <summary>
    /// Reduces a path to the POSIX form this filesystem models.
    ///
    /// The separator is unified rather than left to <see cref="Path"/> because the production code
    /// composes its paths with <see cref="Path.Combine(string, string)"/>, which yields a backslash
    /// on Windows: an XKB root written "/usr/share/X11/xkb" becomes "/usr/share/X11/xkb\symbols"
    /// there, and an ordinal lookup against the forward-slash form a test wrote would miss. The
    /// separator a host happens to prefer is not the thing under test, and a fake that changes
    /// shape with the host turns every import test into a Linux-only one.
    /// </summary>
    private static string Normalize(string path)
    {
        var unified = path.Replace('\\', '/');
        return unified.Length > 1 ? unified.TrimEnd('/') : unified;
    }

    private static string? Parent(string path)
    {
        var normalized = Normalize(path);
        var separator = normalized.LastIndexOf('/');

        return separator switch
        {
            < 0 => null,
            // The root is its own last component and has no parent above it.
            0 => normalized.Length == 1 ? null : "/",
            _ => normalized[..separator]
        };
    }
}
