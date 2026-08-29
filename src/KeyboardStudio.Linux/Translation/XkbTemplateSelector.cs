namespace KeyboardStudio.Linux;

/// <summary>
/// Selects a physical keyboard template for an imported layout, bridging the gap between the XKB
/// registry's country information and the physical geometry that a layout expects.
///
/// The presence of <c>&lt;LSGT&gt;</c> would be a perfect indicator, but it comes from the <c>pc</c>
/// component which import does not compose. Instead, the selector uses country codes from the registry
/// as a fallback, with Europe and most countries preferring iso-105 and North America preferring
/// ansi-104. The suggestion is user-overridable because physical geometry is not recorded in the XKB
/// database; a layout that primarily targets one geometry may still work on another.
/// </summary>
public static class XkbTemplateSelector
{
    /// <summary>ISO 3166 country codes that prefer the ANSI-104 layout.</summary>
    private static readonly HashSet<string> Ansi104Countries = new(StringComparer.Ordinal)
    {
        "US", // United States
        "CA", // Canada
        "MX", // Mexico
        "JP", // Japan
        "BR", // Brazil
        "TW", // Taiwan
        "HK"  // Hong Kong
    };

    /// <summary>
    /// Suggests a template for a layout, in order: <c>&lt;LSGT&gt;</c> presence, registry country
    /// code, or a sensible default.
    /// </summary>
    /// <param name="symbols">The resolved XKB symbols.</param>
    /// <param name="registryEntry">The registry entry, or <see langword="null"/> if not available.</param>
    /// <returns><c>"iso-105"</c> or <c>"ansi-104"</c>.</returns>
    public static string SelectTemplate(ResolvedXkbSymbols symbols, XkbRegistryEntry? registryEntry)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        // If <LSGT> is present, the layout almost certainly expects iso-105, even though it came
        // from the `pc` component rather than `latin`. Layouts that explicitly write it know what
        // they are doing.
        if (symbols.Keys.Any(k => k.KeyName == "<LSGT>"))
        {
            return "iso-105";
        }

        // Prefer the registry's country hint if available.
        if (registryEntry?.Countries is { Count: > 0 })
        {
            // If any of the countries prefer ANSI-104, use that. This is a simplification but works
            // for the real-world layouts: a keyboard layout is usually bound to a single country
            // (or to a region that has consistent preference).
            if (registryEntry.Countries.Any(c => Ansi104Countries.Contains(c)))
            {
                return "ansi-104";
            }

            return "iso-105";
        }

        // No country hint and no <LSGT>; default to iso-105 as it is more commonly used globally.
        return "iso-105";
    }
}
