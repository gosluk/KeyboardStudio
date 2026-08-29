using System.Xml;
using System.Xml.Linq;

namespace KeyboardStudio.Linux;

/// <summary>
/// Reads <c>rules/evdev.xml</c> and <c>rules/evdev.extras.xml</c>, the XML registries every
/// distribution ships alongside the symbols files.
/// </summary>
public sealed class XkbRulesRegistryReader : IXkbLayoutRegistryReader
{
    /// <summary>
    /// The base registry first, then the extras file, so a name defined in both keeps the
    /// description users are more likely to recognize.
    /// </summary>
    private static readonly string[] RegistryFileNames = ["evdev.xml", "evdev.extras.xml"];

    /// <summary>
    /// The registry declares <c>&lt;!DOCTYPE xkbConfigRegistry SYSTEM "xkb.dtd"&gt;</c>. Ignoring
    /// the DTD and refusing a resolver is not tidiness: the alternative fetches an external entity
    /// from a path this application does not own.
    /// </summary>
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        CloseInput = true
    };

    private readonly IXkbFileSystem _fileSystem;

    public XkbRulesRegistryReader(IXkbFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        _fileSystem = fileSystem;
    }

    public IReadOnlyList<XkbRegistryEntry> Read(XkbDataRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var entries = new List<XkbRegistryEntry>();
        var seen = new HashSet<(string Layout, string? Variant)>();

        foreach (var fileName in RegistryFileNames)
        {
            var path = Path.Combine(root.RulesDirectory, fileName);
            if (!_fileSystem.FileExists(path))
            {
                continue;
            }

            foreach (var entry in ReadFile(path))
            {
                if (seen.Add((entry.LayoutId, entry.VariantId)))
                {
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }

    private List<XkbRegistryEntry> ReadFile(string path)
    {
        using var stream = _fileSystem.OpenRead(path);
        using var reader = XmlReader.Create(stream, ReaderSettings);
        var document = XDocument.Load(reader);

        var entries = new List<XkbRegistryEntry>();
        var layouts = document.Root?.Element("layoutList")?.Elements("layout") ?? [];

        foreach (var layout in layouts)
        {
            var configItem = layout.Element("configItem");
            var layoutId = Text(configItem?.Element("name"));
            if (layoutId is null)
            {
                continue;
            }

            var languages = Codes(configItem, "languageList", "iso639Id");
            var countries = Codes(configItem, "countryList", "iso3166Id");

            // The layout itself is importable, not just its variants: it resolves to the symbols
            // file's `default` section.
            entries.Add(new XkbRegistryEntry(
                layoutId,
                VariantId: null,
                DisplayName: Text(configItem?.Element("description")) ?? layoutId,
                ShortDescription: Text(configItem?.Element("shortDescription")),
                languages,
                countries));

            var variants = layout.Element("variantList")?.Elements("variant") ?? [];
            foreach (var variant in variants)
            {
                var variantConfig = variant.Element("configItem");
                var variantId = Text(variantConfig?.Element("name"));
                if (variantId is null)
                {
                    continue;
                }

                // A variant that lists no languages or countries of its own serves the same ones
                // its layout does; inheriting them keeps search working for the majority of
                // variants, which say nothing.
                var variantLanguages = Codes(variantConfig, "languageList", "iso639Id");
                var variantCountries = Codes(variantConfig, "countryList", "iso3166Id");

                entries.Add(new XkbRegistryEntry(
                    layoutId,
                    variantId,
                    DisplayName: Text(variantConfig?.Element("description")) ?? $"{layoutId} ({variantId})",
                    ShortDescription: Text(variantConfig?.Element("shortDescription")),
                    variantLanguages.Count > 0 ? variantLanguages : languages,
                    variantCountries.Count > 0 ? variantCountries : countries));
            }
        }

        return entries;
    }

    private static string? Text(XElement? element)
    {
        var value = element?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static IReadOnlyList<string> Codes(XElement? configItem, string listName, string itemName)
    {
        var list = configItem?.Element(listName);
        if (list is null)
        {
            return [];
        }

        return [.. list.Elements(itemName).Select(Text).OfType<string>()];
    }
}
