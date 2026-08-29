using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// One choice within a layout: its default form, or one of the variants a distribution ships
/// alongside it.
/// </summary>
public sealed class ImportableVariantViewModel
{
    public ImportableVariantViewModel(ImportableLayoutDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Descriptor = descriptor;

        // The registry names a variant fully — "Polish (QWERTZ)" rather than "QWERTZ" — so the
        // variant rows read on their own. The layout's own entry has no such name to show, and is
        // labelled by what it is instead.
        DisplayName = descriptor.VariantId is null ? "Default" : descriptor.DisplayName;
    }

    public ImportableLayoutDescriptor Descriptor { get; }

    public string? VariantId => Descriptor.VariantId;

    public string DisplayName { get; }
}
