using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// A section with all of its includes resolved and every definition merged — the flat view of a
/// layout that the importer turns into key mappings.
/// </summary>
/// <param name="Path">Absolute path of the file the requested section came from.</param>
/// <param name="Section">The requested section's name.</param>
/// <param name="DisplayName">
/// The group-1 <c>name</c>, inherited through includes, or <see langword="null"/> when nothing in
/// the chain set one.
/// </param>
/// <param name="Keys">The merged keys in the order they were first defined.</param>
/// <param name="IncludeChain">
/// Every <c>file(section)</c> that contributed, in the order it was visited: the common base first
/// where one was composed, then the requested section and everything it includes. Composition
/// routinely runs several levels deep, so this is what lets a fidelity report say where a layout
/// actually came from.
/// </param>
/// <param name="Diagnostics">Everything lost or approximated while resolving.</param>
public sealed record ResolvedXkbSymbols(
    string Path,
    string Section,
    string? DisplayName,
    IReadOnlyList<ResolvedXkbKey> Keys,
    IReadOnlyList<string> IncludeChain,
    IReadOnlyList<LayoutImportDiagnostic> Diagnostics)
{
    /// <summary>
    /// The <c>file(section)</c> that was asked for, written the way the include chain writes its
    /// entries. This is the layout, not whatever was composed under it, so it is what an imported
    /// document records as the thing it came from.
    /// </summary>
    public string Origin => $"{System.IO.Path.GetFileName(Path)}({Section})";
}
