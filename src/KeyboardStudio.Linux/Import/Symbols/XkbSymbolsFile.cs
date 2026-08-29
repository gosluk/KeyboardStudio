using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// A parsed <c>symbols/</c> file: its sections, and everything the reader could not use.
/// </summary>
/// <param name="Path">
/// Where the file was read from. Carried because include cycle detection keys on the resolved path
/// together with a section name, and because the include chain is reported back to the user.
/// </param>
/// <param name="Sections">The file's sections in source order.</param>
/// <param name="Diagnostics">
/// What was dropped while reading. Parsing never fails, so this is the only record of the
/// difference between the file and what the parser returned.
/// </param>
public sealed record XkbSymbolsFile(
    string Path,
    IReadOnlyList<XkbSymbolsSection> Sections,
    IReadOnlyList<LayoutImportDiagnostic> Diagnostics)
{
    /// <summary>
    /// The section a bare <c>include "file"</c> or a variant-less import resolves to: the one
    /// flagged <c>default</c>, or the first when none is flagged, which is how libxkbcommon behaves.
    /// Null only when the file defines no sections at all.
    /// </summary>
    public XkbSymbolsSection? DefaultSection =>
        Sections.FirstOrDefault(section => section.IsDefault)
        ?? (Sections.Count > 0 ? Sections[0] : null);

    /// <summary>Finds a section by name, or returns null when the file does not define it.</summary>
    public XkbSymbolsSection? FindSection(string name) =>
        Sections.FirstOrDefault(section => string.Equals(section.Name, name, StringComparison.Ordinal));
}
