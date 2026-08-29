namespace KeyboardStudio.Linux;

/// <summary>
/// An include resolver that answers one file name with one absolute path and defers every other
/// name to the roots behind it.
///
/// This is what lets a symbols file living outside any XKB root be imported. The file the user
/// picked is reachable under the name it has on disk, while the definitions it composes from —
/// <c>latin</c>, <c>us</c>, <c>level3</c> — still resolve out of the installed database, which is
/// the only place they exist. Without the second half, a loose file would import as whatever it
/// overrides and nothing it inherits.
/// </summary>
public sealed class XkbPinnedFileIncludeResolver : IXkbIncludeResolver
{
    private readonly IXkbIncludeResolver _inner;
    private readonly string _fileName;
    private readonly string _path;

    /// <param name="inner">Resolves every name but the pinned one, normally over the host's roots.</param>
    /// <param name="fileName">The name the pinned file answers to, as an include would write it.</param>
    /// <param name="path">Absolute path of the pinned file.</param>
    public XkbPinnedFileIncludeResolver(IXkbIncludeResolver inner, string fileName, string path)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _inner = inner;
        _fileName = fileName;
        _path = path;
    }

    /// <inheritdoc />
    public IReadOnlyList<XkbIncludeSpec> Parse(string specification, XkbMergeMode merge) =>
        _inner.Parse(specification, merge);

    /// <inheritdoc />
    /// <remarks>
    /// The pinned name wins over any root that also defines it. A user's own copy of <c>pl</c>
    /// shadowing the distribution's is exactly what an XKB root of one's own does, so a file
    /// picked by hand behaves the same way.
    /// </remarks>
    public string? ResolveFilePath(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return string.Equals(file, _fileName, StringComparison.Ordinal)
            ? _path
            : _inner.ResolveFilePath(file);
    }
}
