using System.Text;

namespace KeyboardStudio.Linux;

/// <summary>Generates thin public-variant sections for one existing system layout.</summary>
public static class XkbLanguageBridgeGenerator
{
    public static string Generate(string baseLayoutId, IReadOnlyList<XkbUserVariantLayout> layouts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseLayoutId);
        ArgumentNullException.ThrowIfNull(layouts);

        var builder = new StringBuilder()
            .AppendLine("// KeyboardStudio user-variant bridges. Managed blocks only.")
            .AppendLine();

        foreach (var layout in layouts.OrderBy(
                     layout => layout.Metadata.PublicVariantId,
                     StringComparer.Ordinal))
        {
            var metadata = layout.Metadata;
            if (!string.Equals(metadata.BaseLayoutId, baseLayoutId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Variant '{metadata.PublicVariantId}' belongs to '{metadata.BaseLayoutId}', not '{baseLayoutId}'.",
                    nameof(layouts));
            }

            builder.Append("// BEGIN KeyboardStudio ")
                .AppendLine(metadata.ProjectInstallationId)
                .AppendLine("partial alphanumeric_keys")
                .Append("xkb_symbols \"")
                .Append(metadata.PublicVariantId)
                .AppendLine("\" {")
                .Append("    include \"keyboardstudio(")
                .Append(metadata.InternalSectionId)
                .AppendLine(")\"")
                .AppendLine("};")
                .Append("// END KeyboardStudio ")
                .AppendLine(metadata.ProjectInstallationId)
                .AppendLine();
        }

        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
    }
}
