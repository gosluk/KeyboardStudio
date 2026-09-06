using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// One <c>xkb_symbols "name" { ... }</c> block. A symbols file holds several — <c>pl</c> defines
/// <c>basic</c>, <c>legacy</c>, <c>qwertz</c>, and more — and an include names one of them.
/// </summary>
/// <param name="Name">The quoted section name, which is what a variant identifier resolves to.</param>
/// <param name="IsDefault">
/// Whether the section carries the <c>default</c> flag, making it the target of a bare
/// <c>include "file"</c> and of an import that names no variant.
/// </param>
/// <param name="IsPartial">Whether the section defines only some of the keyboard, as most do.</param>
/// <param name="IsHidden">Whether the section is an implementation detail not offered to users.</param>
/// <param name="Statements">The section's statements, in source order, which merges depend on.</param>
/// <param name="Diagnostics">
/// What was dropped while reading this section. Findings belong to the section rather than the file
/// because a file holds sections no layout composes: <c>keypad</c> is read whole and only
/// <c>keypad(x11)</c> contributes, so its unused overlay sections must not report losses against a
/// layout that never merged them.
/// </param>
public sealed record XkbSymbolsSection(
    string Name,
    bool IsDefault,
    bool IsPartial,
    bool IsHidden,
    IReadOnlyList<XkbSymbolsStatement> Statements,
    IReadOnlyList<LayoutImportDiagnostic> Diagnostics);
