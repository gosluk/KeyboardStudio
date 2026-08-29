namespace KeyboardStudio.Linux;

/// <summary>
/// Turns an include string into the files it names, and finds those files on disk.
/// </summary>
public interface IXkbIncludeResolver
{
    /// <summary>
    /// Splits an include specification into its parts. Never fails: a specification that parses to
    /// nothing usable yields an empty list, and the caller reports it against the statement it came
    /// from, where the line number is known.
    /// </summary>
    IReadOnlyList<XkbIncludeSpec> Parse(string specification, XkbMergeMode merge);

    /// <summary>
    /// The absolute path of a symbols file, searching the roots in precedence order, or
    /// <see langword="null"/> when no root holds it.
    /// </summary>
    string? ResolveFilePath(string file);
}
