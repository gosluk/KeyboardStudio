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
    /// <summary>The <c>rules/</c> subdirectory, where the registry files live.</summary>
    public string RulesDirectory => System.IO.Path.Combine(Path, "rules");

    /// <summary>The <c>symbols/</c> subdirectory, where one file per layout lives.</summary>
    public string SymbolsDirectory => System.IO.Path.Combine(Path, "symbols");
}
