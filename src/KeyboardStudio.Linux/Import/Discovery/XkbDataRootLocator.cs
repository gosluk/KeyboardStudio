using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Resolves XKB data roots in libxkbcommon's precedence order. The environment and the filesystem
/// arrive as constructor arguments so the ordering can be tested without a real XKB installation
/// and without mutating the variables of the test host.
/// </summary>
public sealed class XkbDataRootLocator : IXkbDataRootLocator
{
    private static readonly string[] SystemRoots =
    [
        "/etc/xkb",
        "/usr/share/X11/xkb",
        "/usr/local/share/X11/xkb"
    ];

    private readonly IXkbEnvironment _environment;
    private readonly IXkbFileSystem _fileSystem;

    public XkbDataRootLocator(IXkbEnvironment environment, IXkbFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _environment = environment;
        _fileSystem = fileSystem;
    }

    public IReadOnlyList<XkbDataRoot> Locate()
    {
        var roots = new List<XkbDataRoot>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // An explicitly configured root outranks everything, including the user's own directory:
        // whoever set the variable meant to redirect the whole search. It is treated as a system
        // database because it names a database rather than the layouts this user wrote.
        Add(_environment.GetVariable("XKB_CONFIG_ROOT"), LayoutSourceOrigin.System);
        Add(UserConfigRoot(), LayoutSourceOrigin.User);

        foreach (var systemRoot in SystemRoots)
        {
            Add(systemRoot, LayoutSourceOrigin.System);
        }

        return roots;

        void Add(string? path, LayoutSourceOrigin origin)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path))
            {
                return;
            }

            var normalized = System.IO.Path.TrimEndingDirectorySeparator(path.Trim());
            if (!seen.Add(normalized) || !_fileSystem.DirectoryExists(normalized))
            {
                return;
            }

            roots.Add(new XkbDataRoot(normalized, origin));
        }
    }

    /// <summary>
    /// <c>${XDG_CONFIG_HOME:-$HOME/.config}/xkb</c>. A relative <c>XDG_CONFIG_HOME</c> is invalid
    /// per the base-directory specification and is ignored rather than resolved against the working
    /// directory, which has nothing to do with the user's configuration.
    /// </summary>
    private string? UserConfigRoot()
    {
        var configHome = _environment.GetVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(configHome) && System.IO.Path.IsPathRooted(configHome.Trim()))
        {
            return System.IO.Path.Combine(configHome.Trim(), "xkb");
        }

        var home = _environment.GetVariable("HOME");
        return string.IsNullOrWhiteSpace(home)
            ? null
            : System.IO.Path.Combine(home.Trim(), ".config", "xkb");
    }
}
