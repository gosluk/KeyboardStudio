namespace KeyboardStudio.Linux;

/// <summary>
/// One layout or variant as described by <c>rules/evdev.xml</c>. The registry is the only place the
/// XKB database records human-readable names, so an entry is what makes a layout presentable in a
/// list of several hundred.
/// </summary>
/// <param name="LayoutId">The <c>&lt;name&gt;</c> of the layout, such as <c>pl</c>.</param>
/// <param name="VariantId">
/// The <c>&lt;name&gt;</c> of the variant, or <see langword="null"/> for the layout itself, which
/// resolves to its symbols file's <c>default</c> section.
/// </param>
/// <param name="DisplayName">The registry's description, already localized by the distribution.</param>
/// <param name="ShortDescription">The registry's short description, usually a language tag.</param>
/// <param name="Languages">ISO 639 codes the entry serves.</param>
/// <param name="Countries">ISO 3166 codes the entry serves.</param>
public sealed record XkbRegistryEntry(
    string LayoutId,
    string? VariantId,
    string DisplayName,
    string? ShortDescription,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Countries);
