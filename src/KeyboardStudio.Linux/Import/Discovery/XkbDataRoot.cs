using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// One directory holding an XKB database — the thing <c>/usr/share/X11/xkb</c> is. Several may be
/// present at once, and libxkbcommon's search order decides which definition of a name wins.
/// </summary>
/// <param name="Path">Absolute path of the root directory, without a trailing separator.</param>
/// <param name="Origin">
/// Whether the root holds layouts the system installed or ones belonging to the current user. It
/// travels into <see cref="ImportableLayoutDescriptor.Origin"/> so the catalog can group by it.
/// </param>
public sealed record XkbDataRoot(string Path, LayoutSourceOrigin Origin)
{
    // The subdirectories are joined with a forward slash rather than composed with Path.Combine.
    // An XKB database is a POSIX path space by specification — libxkbcommon reads /usr/share/X11/xkb
    // and includes name their files with forward slashes — so the separator is a property of the
    // thing being addressed, not of the host doing the addressing. Deferring to the host emits
    // "/usr/share/X11/xkb\symbols" on Windows, which is neither a path this database uses nor one
    // any XKB tool would write.

    /// <summary>The <c>rules/</c> subdirectory, where the registry files live.</summary>
    public string RulesDirectory => $"{Path}/rules";

    /// <summary>The <c>symbols/</c> subdirectory, where one file per layout lives.</summary>
    public string SymbolsDirectory => $"{Path}/symbols";
}
