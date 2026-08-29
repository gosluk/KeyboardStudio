namespace KeyboardStudio.Linux;

/// <summary>
/// Reads the host's configured layout from the environment and from the files the system keyboard
/// setting is normally written to, in the order the running session would honour them.
///
/// Everything here is a file or an environment read. No process is spawned: asking the running
/// session directly would mean talking to a display server that may not exist, on a machine where
/// the application may be the first thing started, and would make the answer depend on which
/// desktop happens to be installed. The files are where the setting is stored, and they are
/// readable whether or not anything is running.
/// </summary>
public sealed class XkbActiveLayoutProbe : IXkbActiveLayoutProbe
{
    /// <summary>Where systemd-localed writes the X server's keyboard configuration.</summary>
    public const string XorgKeyboardConfigurationPath = "/etc/X11/xorg.conf.d/00-keyboard.conf";

    /// <summary>Where systemd records the system-wide keyboard setting.</summary>
    public const string VirtualConsoleConfigurationPath = "/etc/vconsole.conf";

    /// <summary>The Debian family's equivalent of <see cref="VirtualConsoleConfigurationPath"/>.</summary>
    public const string KeyboardDefaultsPath = "/etc/default/keyboard";

    private readonly IXkbEnvironment _environment;
    private readonly IXkbFileSystem _fileSystem;

    public XkbActiveLayoutProbe(IXkbEnvironment environment, IXkbFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _environment = environment;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The order matches how far each setting reaches. The environment overrides everything for
    /// this process alone and is what a user running the application with a layout in mind will
    /// set. The X keyboard configuration is what a graphical session is actually using. The two
    /// system files are what the machine was set up with, and are consulted last because a
    /// graphical session may well have moved on from them.
    /// </remarks>
    public XkbActiveLayout Detect() =>
        FromEnvironment()
        ?? FromXorgConfiguration()
        ?? FromVirtualConsole()
        ?? FromKeyboardDefaults()
        ?? XkbActiveLayout.Fallback;

    private XkbActiveLayout? FromEnvironment() =>
        Compose(
            _environment.GetVariable("XKB_DEFAULT_LAYOUT"),
            _environment.GetVariable("XKB_DEFAULT_VARIANT"),
            XkbActiveLayoutOrigin.Environment);

    /// <summary>
    /// Reads <c>Option "XkbLayout" "pl"</c> out of the X keyboard configuration. Only the option
    /// lines are looked at, and only for their two quoted arguments: parsing the section structure
    /// would buy nothing, because a file with more than one keyboard section in it has already
    /// stopped being a thing a single answer can describe.
    /// </summary>
    private XkbActiveLayout? FromXorgConfiguration()
    {
        var lines = ReadLines(XorgKeyboardConfigurationPath);
        if (lines is null)
        {
            return null;
        }

        string? layout = null;
        string? variant = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Option", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var arguments = QuotedArguments(trimmed);
            if (arguments.Count < 2)
            {
                continue;
            }

            if (string.Equals(arguments[0], "XkbLayout", StringComparison.OrdinalIgnoreCase))
            {
                layout = arguments[1];
            }
            else if (string.Equals(arguments[0], "XkbVariant", StringComparison.OrdinalIgnoreCase))
            {
                variant = arguments[1];
            }
        }

        return Compose(layout, variant, XkbActiveLayoutOrigin.XorgConfiguration);
    }

    /// <summary>
    /// Reads <c>/etc/vconsole.conf</c>, preferring <c>XKBLAYOUT</c> over <c>KEYMAP</c>.
    /// </summary>
    /// <remarks>
    /// Recent systemd records the X keyboard configuration in this file alongside the console
    /// keymap, and the two are not the same vocabulary: <c>XKBLAYOUT</c> is an XKB layout name,
    /// while <c>KEYMAP</c> names a console keymap that only sometimes coincides with one — a host
    /// set to <c>KEYMAP=pl2</c> has no XKB layout called <c>pl2</c>. The console keymap is still
    /// worth reading, because on a host that has only ever been configured for the console it is
    /// the only statement of intent there is; it is simply the weaker of the two.
    /// </remarks>
    private XkbActiveLayout? FromVirtualConsole()
    {
        var assignments = ReadAssignments(VirtualConsoleConfigurationPath);
        if (assignments is null)
        {
            return null;
        }

        return Compose(
                   assignments.GetValueOrDefault("XKBLAYOUT"),
                   assignments.GetValueOrDefault("XKBVARIANT"),
                   XkbActiveLayoutOrigin.VirtualConsole)
               ?? Compose(
                   assignments.GetValueOrDefault("KEYMAP"),
                   variant: null,
                   XkbActiveLayoutOrigin.VirtualConsole);
    }

    private XkbActiveLayout? FromKeyboardDefaults()
    {
        var assignments = ReadAssignments(KeyboardDefaultsPath);
        return assignments is null
            ? null
            : Compose(
                assignments.GetValueOrDefault("XKBLAYOUT"),
                assignments.GetValueOrDefault("XKBVARIANT"),
                XkbActiveLayoutOrigin.KeyboardDefaults);
    }

    /// <summary>
    /// Builds a result from one setting's layout and variant, or returns null when that setting
    /// said nothing and the next one down should be asked.
    /// </summary>
    /// <remarks>
    /// Both values may be comma-separated lists of the layouts a session switches between, with
    /// the variants positional against them — <c>us,pl</c> with <c>,dvorak</c> means an unmodified
    /// <c>us</c> first and <c>pl(dvorak)</c> second. The first entry is the one the session starts
    /// in, so that is the pair taken, and an empty variant in that position means the layout's own
    /// default rather than a variant named by the empty string.
    /// </remarks>
    private static XkbActiveLayout? Compose(string? layout, string? variant, XkbActiveLayoutOrigin origin)
    {
        var layoutId = FirstEntry(layout);
        return layoutId is null
            ? null
            : new XkbActiveLayout(layoutId, FirstEntry(variant), origin);
    }

    private static string? FirstEntry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var separator = value.IndexOf(',', StringComparison.Ordinal);
        var first = (separator < 0 ? value : value[..separator]).Trim();
        return first.Length == 0 ? null : first;
    }

    /// <summary>
    /// The double-quoted arguments on one line, in order. Escapes are not interpreted: nothing that
    /// appears in a layout or variant name needs them.
    /// </summary>
    private static List<string> QuotedArguments(string line)
    {
        var arguments = new List<string>(2);
        var index = 0;

        while (true)
        {
            var open = line.IndexOf('"', index);
            if (open < 0)
            {
                break;
            }

            var close = line.IndexOf('"', open + 1);
            if (close < 0)
            {
                break;
            }

            arguments.Add(line[(open + 1)..close]);
            index = close + 1;
        }

        return arguments;
    }

    /// <summary>
    /// Reads a shell-style <c>KEY=value</c> configuration file. Both files this is used on are
    /// sourced by shell scripts, but only assignments of literal words matter here, so quotes are
    /// stripped and nothing else is interpreted.
    /// </summary>
    private Dictionary<string, string>? ReadAssignments(string path)
    {
        var lines = ReadLines(path);
        if (lines is null)
        {
            return null;
        }

        var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            var separator = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();

            if (value.Length >= 2 &&
                (value[0] == '"' || value[0] == '\'') &&
                value[^1] == value[0])
            {
                value = value[1..^1];
            }

            // Last assignment wins, as it would were the file sourced.
            assignments[key] = value;
        }

        return assignments;
    }

    /// <summary>
    /// The file's lines, or null when there is no file or it cannot be read. An unreadable file is
    /// not distinguished from a missing one: either way this step has nothing to contribute, and
    /// the next one down is a better answer than an error nobody asked a question to get.
    /// </summary>
    private List<string>? ReadLines(string path)
    {
        if (!_fileSystem.FileExists(path))
        {
            return null;
        }

        try
        {
            using var stream = _fileSystem.OpenRead(path);
            using var reader = new StreamReader(stream);

            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            return lines;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
