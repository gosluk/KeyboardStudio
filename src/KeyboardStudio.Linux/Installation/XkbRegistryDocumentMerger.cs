using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace KeyboardStudio.Linux;

/// <summary>Merges owned variant entries into a shared registry with external resolution disabled.</summary>
public static class XkbRegistryDocumentMerger
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        CloseInput = true
    };

    public static XkbRegistryMergeResult Upsert(
        string? existingContent,
        XkbUserVariantMetadata metadata,
        string? expectedExistingEntrySha256)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var diagnostics = new List<XkbDiagnostic>();
        var document = Parse(existingContent, diagnostics);
        if (document is null)
        {
            return Failed(existingContent, diagnostics);
        }

        var layoutList = EnsureLayoutList(document.Root!);
        var layout = FindLayout(layoutList, metadata.BaseLayoutId);
        var variant = layout is null ? null : FindVariant(layout, metadata.PublicVariantId);
        if (variant is not null)
        {
            if (!IsOwned(variant, metadata.ProjectInstallationId))
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSR002",
                    $"Registry variant '{metadata.BaseLayoutId}({metadata.PublicVariantId})' is not owned by this project."));
                return Failed(existingContent, diagnostics);
            }

            var currentHash = HashElement(variant);
            if (expectedExistingEntrySha256 is null ||
                !string.Equals(currentHash, expectedExistingEntrySha256, StringComparison.Ordinal))
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSR003",
                    "The existing managed registry entry was changed outside KeyboardStudio."));
                return Failed(existingContent, diagnostics);
            }

            var replacement = CreateVariant(metadata);
            variant.ReplaceWith(replacement);
            var content = Serialize(document);
            return Succeeded(content, replacement, !string.Equals(content, existingContent, StringComparison.Ordinal));
        }

        if (expectedExistingEntrySha256 is not null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSR003",
                "The installation manifest expects a registry entry that is missing."));
            return Failed(existingContent, diagnostics);
        }

        layout ??= CreateLayout(layoutList, metadata.BaseLayoutId);
        var variantList = layout.Element("variantList");
        if (variantList is null)
        {
            variantList = new XElement("variantList");
            layout.Add(variantList);
        }

        var created = CreateVariant(metadata);
        variantList.Add(
            new XComment($" BEGIN KeyboardStudio {metadata.ProjectInstallationId} "),
            created,
            new XComment($" END KeyboardStudio {metadata.ProjectInstallationId} "));
        return Succeeded(Serialize(document), created, changed: true);
    }

    public static XkbRegistryMergeResult Remove(
        string existingContent,
        XkbUserVariantMetadata metadata,
        string expectedExistingEntrySha256)
    {
        ArgumentNullException.ThrowIfNull(existingContent);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedExistingEntrySha256);

        var diagnostics = new List<XkbDiagnostic>();
        var document = Parse(existingContent, diagnostics);
        var layoutList = document?.Root?.Element("layoutList");
        var layout = layoutList is null ? null : FindLayout(layoutList, metadata.BaseLayoutId);
        var variant = layout is null ? null : FindVariant(layout, metadata.PublicVariantId);
        if (document is null || variant is null)
        {
            diagnostics.Add(new XkbDiagnostic("KSR003", "The managed registry entry is missing."));
            return Failed(existingContent, diagnostics);
        }

        if (!IsOwned(variant, metadata.ProjectInstallationId) ||
            !string.Equals(HashElement(variant), expectedExistingEntrySha256, StringComparison.Ordinal))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSR003",
                "The existing managed registry entry was changed outside KeyboardStudio."));
            return Failed(existingContent, diagnostics);
        }

        var before = PreviousSignificantNode(variant);
        var after = NextSignificantNode(variant);
        variant.Remove();
        if (IsMarker(before, "BEGIN", metadata.ProjectInstallationId))
        {
            before!.Remove();
        }

        if (IsMarker(after, "END", metadata.ProjectInstallationId))
        {
            after!.Remove();
        }

        var variantList = layout!.Element("variantList");
        if (variantList is not null && !variantList.Elements("variant").Any())
        {
            variantList.Remove();
        }

        if (!layout.Elements().Any(element => element.Name != "configItem") &&
            layout.Element("configItem")?.Elements().All(element => element.Name == "name") == true)
        {
            layout.Remove();
        }

        var empty = !document.Root!.Element("layoutList")!.Elements("layout").Any() &&
                    !document.Root.Elements().Any(element => element.Name != "layoutList");
        return new XkbRegistryMergeResult(
            true,
            empty ? null : Serialize(document),
            null,
            Changed: true,
            []);
    }

    private static XDocument? Parse(string? content, List<XkbDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(
                    "xkbConfigRegistry",
                    new XAttribute("version", "1.1"),
                    new XElement("layoutList")));
        }

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            using var reader = XmlReader.Create(stream, ReaderSettings);
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (document.Root?.Name != "xkbConfigRegistry")
            {
                throw new XmlException("The root element must be xkbConfigRegistry.");
            }

            return document;
        }
        catch (XmlException exception)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSR001",
                $"The user XKB registry is malformed: {exception.Message}"));
            return null;
        }
    }

    private static XElement EnsureLayoutList(XElement root)
    {
        var layoutList = root.Element("layoutList");
        if (layoutList is not null)
        {
            return layoutList;
        }

        layoutList = new XElement("layoutList");
        root.Add(layoutList);
        return layoutList;
    }

    private static XElement? FindLayout(XElement layoutList, string layoutId) =>
        layoutList.Elements("layout").FirstOrDefault(layout =>
            string.Equals(
                layout.Element("configItem")?.Element("name")?.Value.Trim(),
                layoutId,
                StringComparison.Ordinal));

    private static XElement? FindVariant(XElement layout, string variantId) =>
        layout.Element("variantList")?.Elements("variant").FirstOrDefault(variant =>
            string.Equals(
                variant.Element("configItem")?.Element("name")?.Value.Trim(),
                variantId,
                StringComparison.Ordinal));

    private static XElement CreateLayout(XElement layoutList, string layoutId)
    {
        var layout = new XElement(
            "layout",
            new XElement("configItem", new XElement("name", layoutId)),
            new XElement("variantList"));
        layoutList.Add(layout);
        return layout;
    }

    private static XElement CreateVariant(XkbUserVariantMetadata metadata) =>
        new(
            "variant",
            new XElement(
                "configItem",
                new XElement("name", metadata.PublicVariantId),
                new XElement("shortDescription", metadata.BaseLayoutId),
                new XElement("description", metadata.Description)));

    private static bool IsOwned(XElement variant, string id) =>
        IsMarker(PreviousSignificantNode(variant), "BEGIN", id) &&
        IsMarker(NextSignificantNode(variant), "END", id);

    private static XNode? PreviousSignificantNode(XNode node)
    {
        for (var current = node.PreviousNode; current is not null; current = current.PreviousNode)
        {
            if (current is not XText text || !string.IsNullOrWhiteSpace(text.Value))
            {
                return current;
            }
        }

        return null;
    }

    private static XNode? NextSignificantNode(XNode node)
    {
        for (var current = node.NextNode; current is not null; current = current.NextNode)
        {
            if (current is not XText text || !string.IsNullOrWhiteSpace(text.Value))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsMarker(XNode? node, string kind, string id) =>
        node is XComment comment &&
        string.Equals(comment.Value.Trim(), $"{kind} KeyboardStudio {id}", StringComparison.Ordinal);

    private static string HashElement(XElement element) =>
        Hash(element.ToString(SaveOptions.DisableFormatting));

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Serialize(XDocument document)
    {
        var body = document.ToString(SaveOptions.DisableFormatting).Trim();
        if (!body.StartsWith("<?xml", StringComparison.Ordinal))
        {
            body = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + body;
        }

        return body.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static XkbRegistryMergeResult Succeeded(
        string content,
        XElement element,
        bool changed) =>
        new(true, content, HashElement(element), changed, []);

    private static XkbRegistryMergeResult Failed(
        string? content,
        IReadOnlyList<XkbDiagnostic> diagnostics) =>
        new(false, content, null, Changed: false, diagnostics);
}
