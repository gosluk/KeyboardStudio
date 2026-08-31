using System.Text;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// One layout in the import catalog, with the variants that belong to it.
///
/// The catalog arrives flat — one descriptor per layout-and-variant pair, several hundred of them —
/// which is the right shape to import from and the wrong shape to choose from. Grouping the
/// variants under their layout turns a list nobody can scan into a list of a hundred or so
/// countries, each of which opens.
/// </summary>
public sealed class ImportableLayoutViewModel
{
    private readonly string _searchIndex;

    /// <param name="layoutId">The identifier every descriptor in the group shares.</param>
    /// <param name="descriptors">Every descriptor for that layout, in any order.</param>
    public ImportableLayoutViewModel(
        string layoutId,
        IReadOnlyList<ImportableLayoutDescriptor> descriptors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        ArgumentNullException.ThrowIfNull(descriptors);

        if (descriptors.Count == 0)
        {
            throw new ArgumentException(
                $"Layout '{layoutId}' must have at least one descriptor.",
                nameof(descriptors));
        }

        LayoutId = layoutId;

        // The layout's own entry leads, because it is what "pl" means when nothing further is
        // said. The rest follow by the name the list shows rather than by the identifier behind
        // it, so that a drop-down of a dozen variants reads in the order it is displayed in.
        Variants = descriptors
            .Select(descriptor => new ImportableVariantViewModel(descriptor))
            .OrderBy(variant => variant.VariantId is null ? 0 : 1)
            .ThenBy(variant => variant.DisplayName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(variant => variant.VariantId, StringComparer.Ordinal)
            .ToArray();

        var primary = Variants[0].Descriptor;
        DisplayName = primary.VariantId is null ? primary.DisplayName : layoutId;
        Origin = primary.Origin;
        SourceLocation = primary.SourceLocation;
        Detail = Variants.Count > 1
            ? $"{layoutId} · {Variants.Count} variants"
            : layoutId;

        _searchIndex = BuildSearchIndex(layoutId, descriptors);
    }

    public string LayoutId { get; }

    public string DisplayName { get; }

    /// <summary>Secondary line under the name: the bare identifier and how many variants it has.</summary>
    public string Detail { get; }

    public LayoutSourceOrigin Origin { get; }

    public string SourceLocation { get; }

    public IReadOnlyList<ImportableVariantViewModel> Variants { get; }

    /// <summary>
    /// Whether this layout answers a search. The index covers identifiers, names, languages and
    /// countries, so that "polish", "pl" and "pol" all reach the same entry — a user looking for
    /// their own keyboard rarely knows which of the three the distribution used.
    /// </summary>
    public bool Matches(string? search) =>
        string.IsNullOrWhiteSpace(search) ||
        _searchIndex.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string BuildSearchIndex(
        string layoutId,
        IReadOnlyList<ImportableLayoutDescriptor> descriptors)
    {
        var builder = new StringBuilder(layoutId);
        foreach (var descriptor in descriptors)
        {
            builder.Append('\n').Append(descriptor.DisplayName);
            Append(descriptor.VariantId);
            Append(descriptor.ShortDescription);
            foreach (var language in descriptor.Languages)
            {
                Append(language);
            }

            foreach (var country in descriptor.Countries)
            {
                Append(country);
            }
        }

        return builder.ToString();

        void Append(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append('\n').Append(value);
            }
        }
    }
}
