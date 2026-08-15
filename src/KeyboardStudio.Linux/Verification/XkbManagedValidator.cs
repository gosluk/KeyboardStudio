using System.Text.RegularExpressions;

namespace KeyboardStudio.Linux;

public sealed partial class XkbManagedValidator : IXkbManagedValidator
{
    public IReadOnlyList<XkbDiagnostic> Validate(
        XkbKeyboardLayout layout,
        XkbGeneratedSymbols generated)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(generated);

        var diagnostics = new List<XkbDiagnostic>();
        if (!IdentifierPattern().IsMatch(layout.Metadata.LayoutId) ||
            !IdentifierPattern().IsMatch(layout.Metadata.SectionId))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSL003",
                "The XKB layout and section identifiers are not portable identifiers."));
        }

        var expectedPath = Path.Combine("symbols", layout.Metadata.LayoutId);
        if (!string.Equals(generated.RelativePath, expectedPath, StringComparison.Ordinal))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSL003",
                $"The generated symbols path must be '{expectedPath}'."));
        }

        string? previousKeyName = null;
        foreach (var mapping in layout.Mappings)
        {
            if (!KeyNamePattern().IsMatch(mapping.KeyName))
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSL003",
                    $"XKB key name '{mapping.KeyName}' is invalid.",
                    mapping.PhysicalKeyId));
            }

            if (previousKeyName is not null &&
                string.CompareOrdinal(previousKeyName, mapping.KeyName) >= 0)
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSL003",
                    "XKB key declarations must be uniquely sorted by key name.",
                    mapping.PhysicalKeyId));
            }

            previousKeyName = mapping.KeyName;
            if (mapping.Keysyms.Count is < 1 or > 4 ||
                mapping.Keysyms.Any(keysym => !KeysymPattern().IsMatch(keysym)))
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSL003",
                    "An XKB mapping must contain one to four valid keysyms.",
                    mapping.PhysicalKeyId));
            }
        }

        var expectedContent = new XkbSymbolsGenerator().Generate(layout).Content;
        if (!string.Equals(generated.Content, expectedContent, StringComparison.Ordinal))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSL003",
                "The generated XKB component is not the deterministic representation of its model."));
        }

        return diagnostics;
    }

    [GeneratedRegex("^[a-z_][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^<[A-Z0-9]{2,4}>$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyNamePattern();

    [GeneratedRegex("^(NoSymbol|U[0-9A-F]{4,8}|[A-Za-z0-9_]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex KeysymPattern();
}
