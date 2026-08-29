namespace KeyboardStudio.Core;

/// <summary>
/// One entry in the import catalog: a layout that can be imported, described well enough to be
/// searched, grouped, and chosen from a list of several hundred.
///
/// The descriptive fields exist for presentation only. Core does not interpret them, and a source
/// that knows nothing beyond an identifier may repeat it as the display name and leave the rest
/// empty.
/// </summary>
/// <param name="SourceId">Identifies the <see cref="ILayoutImportSource"/> that produced this.</param>
/// <param name="LayoutId">Source-specific layout identifier, such as a country code.</param>
/// <param name="VariantId">
/// Source-specific variant identifier, or <see langword="null"/> for the layout's default variant.
/// </param>
/// <param name="DisplayName">Human-readable name, already localized by the source if it can be.</param>
/// <param name="ShortDescription">Brief secondary label, or <see langword="null"/>.</param>
/// <param name="Languages">Language tags the layout serves, for search. May be empty.</param>
/// <param name="Countries">Country codes the layout serves, for search. May be empty.</param>
/// <param name="Origin">Whether the layout ships with the system, belongs to the user, or is a loose file.</param>
/// <param name="SourceLocation">
/// Where the layout is stored. Shown when two entries are otherwise indistinguishable, and recorded
/// as provenance on the imported document.
/// </param>
public sealed record ImportableLayoutDescriptor(
    string SourceId,
    string LayoutId,
    string? VariantId,
    string DisplayName,
    string? ShortDescription,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Countries,
    LayoutSourceOrigin Origin,
    string SourceLocation)
{
    /// <summary>
    /// Builds the reference that imports this entry. Deriving it here rather than at each call site
    /// keeps a descriptor and the reference that fetches it from drifting apart.
    /// </summary>
    public ImportableLayoutReference ToReference() =>
        new(SourceId, LayoutId, VariantId, SourceLocation);
}
