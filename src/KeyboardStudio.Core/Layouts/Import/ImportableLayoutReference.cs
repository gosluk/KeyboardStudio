namespace KeyboardStudio.Core;

/// <summary>
/// Names one importable layout well enough for its source to fetch it again.
///
/// Every part is an opaque string: Core neither parses nor interprets these identifiers, which is
/// what keeps platform vocabulary out of the domain. A reference usually comes from
/// <see cref="ImportableLayoutDescriptor.ToReference"/>, but it can also be built by hand for a
/// layout that no catalog lists — importing a file the user picked, for instance.
/// </summary>
/// <param name="SourceId">Identifies the <see cref="ILayoutImportSource"/> that can resolve this.</param>
/// <param name="LayoutId">Source-specific layout identifier.</param>
/// <param name="VariantId">
/// Source-specific variant identifier, or <see langword="null"/> for the layout's default variant.
/// </param>
/// <param name="SourceLocation">
/// Where the layout is stored, when the caller already knows. Sources may use it to disambiguate a
/// layout ID that several locations define, or to reach a layout the catalog does not list.
/// </param>
public sealed record ImportableLayoutReference(
    string SourceId,
    string LayoutId,
    string? VariantId = null,
    string? SourceLocation = null);
