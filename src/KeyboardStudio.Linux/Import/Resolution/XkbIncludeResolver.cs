namespace KeyboardStudio.Linux;

/// <summary>
/// Reads include specifications and locates the files they name.
///
/// Parsing is separated from the recursive walk in <see cref="XkbSymbolsResolver"/> because the two
/// fail in different ways and at different costs: a specification this class cannot read is one
/// broken statement, while a file the walk cannot resolve is a missing layout.
/// </summary>
public sealed class XkbIncludeResolver : IXkbIncludeResolver
{
    /// <summary>
    /// The separators that join several includes into one string. Both are merge operators:
    /// <c>+</c> lets the right side win, <c>|</c> lets the left side stand.
    /// </summary>
    private static readonly char[] Separators = ['+', '|'];

    private readonly IXkbFileSystem _fileSystem;
    private readonly IReadOnlyList<XkbDataRoot> _roots;

    public XkbIncludeResolver(IXkbFileSystem fileSystem, IReadOnlyList<XkbDataRoot> roots)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(roots);

        _fileSystem = fileSystem;
        _roots = roots;
    }

    public IReadOnlyList<XkbIncludeSpec> Parse(string specification, XkbMergeMode merge)
    {
        ArgumentNullException.ThrowIfNull(specification);

        var specs = new List<XkbIncludeSpec>();

        // The first piece inherits the statement's own prefix; each later piece takes its rule from
        // the separator that introduced it.
        var pieceMerge = merge;
        var start = 0;

        while (start <= specification.Length)
        {
            var next = specification.IndexOfAny(Separators, start);
            var end = next < 0 ? specification.Length : next;

            var spec = ParsePiece(specification.AsSpan(start, end - start), pieceMerge);
            if (spec is not null)
            {
                specs.Add(spec);
            }

            if (next < 0)
            {
                break;
            }

            pieceMerge = specification[next] == '+' ? XkbMergeMode.Override : XkbMergeMode.Augment;
            start = next + 1;
        }

        return specs;
    }

    public string? ResolveFilePath(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length == 0)
        {
            return null;
        }

        foreach (var root in _roots)
        {
            // The include names a path relative to symbols/, using forward slashes regardless of
            // host convention, and the root it is joined to is a POSIX path for the same reason.
            var candidate = $"{root.SymbolsDirectory}/{file}";
            if (_fileSystem.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads one <c>file(section):group</c> piece. Returns null when nothing names a file, which is
    /// what an empty piece or a stray separator produces.
    /// </summary>
    private static XkbIncludeSpec? ParsePiece(ReadOnlySpan<char> piece, XkbMergeMode merge)
    {
        piece = piece.Trim();
        if (piece.IsEmpty)
        {
            return null;
        }

        // The group suffix sits outside the parentheses, so it is taken off first.
        var group = 1;
        var colon = piece.LastIndexOf(':');
        if (colon >= 0 && int.TryParse(piece[(colon + 1)..], out var parsedGroup))
        {
            group = parsedGroup;
            piece = piece[..colon].TrimEnd();
        }

        string? section = null;
        var open = piece.IndexOf('(');
        if (open >= 0)
        {
            var close = piece.LastIndexOf(')');

            // A missing close parenthesis is treated as running to the end. XKB files are
            // hand-written, and reading the obvious intent beats discarding the whole include.
            var inner = close > open ? piece[(open + 1)..close] : piece[(open + 1)..];
            section = inner.Trim().ToString();
            piece = piece[..open].TrimEnd();

            if (section.Length == 0)
            {
                section = null;
            }
        }

        var file = piece.Trim().ToString();
        return file.Length == 0 ? null : new XkbIncludeSpec(file, section, merge, group);
    }
}
