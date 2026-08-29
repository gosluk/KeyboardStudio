namespace KeyboardStudio.Persistence;

/// <summary>
/// Where an imported document came from.
///
/// Provenance is editor bookkeeping rather than layout semantics, so it lives in the document
/// envelope beside the target profiles instead of in <c>ProjectMetadata</c>. Putting it in the
/// domain would give <see cref="KeyboardStudio.Core.KeyboardProject"/> a field that means nothing
/// to a project the user typed out by hand, and would make Core carry a concept only the
/// application uses.
///
/// Every field is what the source said at the time. None of it is re-read on load: the layout the
/// document was imported from may since have been edited, upgraded, or removed, and a record of
/// where something came from has to keep saying so even when the answer has changed.
/// </summary>
/// <param name="SourceId">The <c>ILayoutImportSource.Id</c> that produced the project.</param>
/// <param name="LayoutId">Source-specific layout identifier, such as <c>pl</c>.</param>
/// <param name="VariantId">Source-specific variant identifier, or null for the default variant.</param>
/// <param name="SourceLocation">Where the layout was stored, when the source knew.</param>
/// <param name="SourceDescription">The layout's display name at import time, when the source had one.</param>
/// <param name="ImportedAtUtc">When the import ran.</param>
public sealed record LayoutImportProvenance(
    string SourceId,
    string LayoutId,
    string? VariantId,
    string? SourceLocation,
    string? SourceDescription,
    DateTimeOffset ImportedAtUtc)
{
    /// <summary>
    /// The layout as a source would name it: <c>pl</c>, or <c>pl(qwertz)</c> when a variant was
    /// chosen. Shown wherever one line has to say what a document was imported from.
    /// </summary>
    public string Describe() =>
        VariantId is null ? LayoutId : $"{LayoutId}({VariantId})";
}
