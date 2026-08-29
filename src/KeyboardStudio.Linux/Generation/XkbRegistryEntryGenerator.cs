using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace KeyboardStudio.Linux;

/// <summary>Generates a minimal user registry overlay grouped under existing layout identifiers.</summary>
public static class XkbRegistryEntryGenerator
{
    public static string Generate(IReadOnlyList<XkbUserVariantLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);

        var layoutElements = layouts
            .GroupBy(layout => layout.Metadata.BaseLayoutId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new XElement(
                "layout",
                new XElement("configItem", new XElement("name", group.Key)),
                new XElement(
                    "variantList",
                    group.OrderBy(
                            layout => layout.Metadata.PublicVariantId,
                            StringComparer.Ordinal)
                        .SelectMany(CreateVariantNodes))))
            .ToArray();

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(
                "xkbConfigRegistry",
                new XAttribute("version", "1.1"),
                new XElement("layoutList", layoutElements)));

        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = true
        }))
        {
            document.Save(writer);
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            builder.ToString().TrimEnd() + "\n";
    }

    private static IEnumerable<XNode> CreateVariantNodes(XkbUserVariantLayout layout)
    {
        var metadata = layout.Metadata;
        yield return new XComment($" BEGIN KeyboardStudio {metadata.ProjectInstallationId} ");
        yield return new XElement(
            "variant",
            new XElement(
                "configItem",
                new XElement("name", metadata.PublicVariantId),
                new XElement("shortDescription", metadata.BaseLayoutId),
                new XElement("description", metadata.Description)));
        yield return new XComment($" END KeyboardStudio {metadata.ProjectInstallationId} ");
    }
}
