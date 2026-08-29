using System.Collections.Frozen;

namespace KeyboardStudio.Linux;

/// <summary>
/// Suggests the physical keyboard an imported layout was written for.
///
/// Nothing in the XKB database records geometry, so this is inference and the import dialog offers
/// it as a suggestion the user can override. Getting it wrong costs one key: the extra key an
/// ISO board has beside the left shift, which an ANSI board does not.
/// </summary>
public static class XkbTemplateSelector
{
    private const string Iso105 = "iso-105";
    private const string Ansi104 = "ansi-104";

    /// <summary>
    /// The key an ISO board has and an ANSI board does not. A layout that writes it needs it.
    /// </summary>
    private const string IsoExtraKey = "<LSGT>";

    /// <summary>
    /// The countries whose keyboards are ANSI, or near enough that ANSI is the better of the two
    /// templates on offer. Japan and Brazil have boards of their own — JIS and ABNT-2 — and are
    /// here because those are ANSI plus extra keys rather than ISO.
    /// </summary>
    private static readonly FrozenSet<string> Ansi104Countries =
        FrozenSet.Create(StringComparer.Ordinal, "US", "CA", "MX", "JP", "BR", "TW", "HK", "KR", "PH");

    /// <summary>
    /// Suggests a template for a resolved layout.
    /// </summary>
    /// <param name="symbols">The flattened layout.</param>
    /// <param name="registryEntry">
    /// What the registry says about the layout, or <see langword="null"/> when it says nothing.
    /// </param>
    /// <returns>The ID of a template <see cref="IKeyboardTemplateProvider"/> can load.</returns>
    public static string SelectTemplate(ResolvedXkbSymbols symbols, XkbRegistryEntry? registryEntry)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        // Writing <LSGT> settles it. It is only a partial signal, though: the key is usually
        // contributed by the `pc` component, which import does not compose, so most ISO layouts
        // reach here without it and the country hint has to carry them.
        if (symbols.Keys.Any(key => string.Equals(key.KeyName, IsoExtraKey, StringComparison.Ordinal)))
        {
            return Iso105;
        }

        // Every country the layout serves has to prefer ANSI before ANSI is suggested. A layout
        // shared between the US and Europe is offered the board that can represent both.
        if (registryEntry?.Countries is { Count: > 0 } countries &&
            countries.All(Ansi104Countries.Contains))
        {
            return Ansi104;
        }

        return Iso105;
    }
}
