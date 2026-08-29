namespace KeyboardStudio.Core;

/// <summary>
/// Caller-supplied choices that steer an import. Every one is optional: a source that is given
/// nothing picks sensible values and reports what it picked.
/// </summary>
/// <param name="TemplateId">
/// Physical keyboard template to import onto. When <see langword="null"/> the source infers one
/// from the layout and returns it as <see cref="LayoutImportResult.SuggestedTemplateId"/>. The
/// import dialog shows that suggestion and lets the user override it here, because the geometry a
/// layout expects is rarely recorded anywhere and the inference will sometimes be wrong.
/// </param>
/// <param name="ProjectName">
/// Name for the imported project. When <see langword="null"/> the source derives one from the
/// layout's own description.
/// </param>
public sealed record LayoutImportOptions(
    string? TemplateId = null,
    string? ProjectName = null)
{
    /// <summary>Leaves every choice to the source.</summary>
    public static LayoutImportOptions Default { get; } = new();
}
