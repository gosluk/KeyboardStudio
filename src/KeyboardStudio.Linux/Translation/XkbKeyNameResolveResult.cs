using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// What one XKB key name became.
/// </summary>
/// <param name="KeyId">
/// The physical key of the chosen template, or <see langword="null"/> when the template has no such
/// key. Unlike the keysym decoder, no outcome is carried alongside: a name either names a key of
/// this template or it does not, and the single reason it can fail is the single code it is
/// reported under.
/// </param>
/// <param name="Diagnostic">
/// Why the key was skipped, or <see langword="null"/> when it was not. Info rather than a warning:
/// a definition naming keys the chosen keyboard lacks is the normal state of affairs, not a fault.
/// </param>
public sealed record XkbKeyNameResolveResult(string? KeyId, LayoutImportDiagnostic? Diagnostic)
{
    /// <summary>Whether the name landed on a key of the template.</summary>
    public bool Resolved => KeyId is not null;
}
