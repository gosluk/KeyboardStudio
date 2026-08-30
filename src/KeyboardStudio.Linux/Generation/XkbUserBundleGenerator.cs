using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KeyboardStudio.Linux;

/// <summary>Composes deterministic generated files for an isolated per-user XKB root.</summary>
public static class XkbUserBundleGenerator
{
    public const string GeneratorVersion = "1.0";
    public const string InternalSectionCollisionCode = "KSB001";
    public const string PublicVariantCollisionCode = "KSB002";

    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static XkbUserBundleGenerationResult Generate(IReadOnlyList<XkbUserVariantLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);

        var ordered = layouts
            .OrderBy(layout => layout.Metadata.BaseLayoutId, StringComparer.Ordinal)
            .ThenBy(layout => layout.Metadata.PublicVariantId, StringComparer.Ordinal)
            .ThenBy(layout => layout.Metadata.ProjectInstallationId, StringComparer.Ordinal)
            .ToArray();
        var diagnostics = ValidateIdentifiers(ordered);
        if (diagnostics.Count > 0)
        {
            return new XkbUserBundleGenerationResult(false, null, diagnostics);
        }

        var files = new List<XkbUserBundleFile>
        {
            CreateFile("symbols/keyboardstudio", XkbUserVariantSymbolsGenerator.Generate(ordered))
        };

        foreach (var group in ordered.GroupBy(
                     layout => layout.Metadata.BaseLayoutId,
                     StringComparer.Ordinal))
        {
            files.Add(CreateFile(
                $"symbols/{group.Key}",
                XkbLanguageBridgeGenerator.Generate(group.Key, group.ToArray())));
        }

        files.Add(CreateFile("rules/evdev.xml", XkbRegistryEntryGenerator.Generate(ordered)));
        files.Add(CreateFile(
            "keyboardstudio-bundle.json",
            GenerateManifest(ordered, files)));

        return new XkbUserBundleGenerationResult(
            true,
            new XkbGeneratedUserBundle(files.AsReadOnly()),
            []);
    }

    private static ReadOnlyCollection<XkbDiagnostic> ValidateIdentifiers(
        IReadOnlyList<XkbUserVariantLayout> layouts)
    {
        var diagnostics = new List<XkbDiagnostic>();
        foreach (var group in layouts.GroupBy(
                     layout => layout.Metadata.InternalSectionId,
                     StringComparer.Ordinal))
        {
            if (group.Select(layout => layout.Metadata.ProjectInstallationId)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any())
            {
                diagnostics.Add(new XkbDiagnostic(
                    InternalSectionCollisionCode,
                    $"More than one project resolves to internal section '{group.Key}'."));
            }
        }

        foreach (var group in layouts.GroupBy(
                     layout => (
                         layout.Metadata.BaseLayoutId,
                         layout.Metadata.PublicVariantId)))
        {
            if (group.Skip(1).Any())
            {
                diagnostics.Add(new XkbDiagnostic(
                    PublicVariantCollisionCode,
                    $"More than one project claims '{group.Key.BaseLayoutId}({group.Key.PublicVariantId})'."));
            }
        }

        return diagnostics.AsReadOnly();
    }

    private static XkbUserBundleFile CreateFile(string relativePath, string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        return new XkbUserBundleFile(relativePath, normalized, Hash(normalized));
    }

    private static string GenerateManifest(
        IReadOnlyList<XkbUserVariantLayout> layouts,
        IReadOnlyList<XkbUserBundleFile> files)
    {
        var manifest = new BundleManifest(
            SchemaVersion: 1,
            GeneratorVersion,
            layouts.Select(layout => new BundleVariant(
                    layout.Metadata.ProjectInstallationId,
                    layout.Metadata.BaseLayoutId,
                    layout.Metadata.BaseVariantId,
                    layout.Metadata.ResolvedBaseSectionId,
                    layout.Metadata.PublicVariantId,
                    layout.Metadata.InternalSectionId,
                    layout.Metadata.Description,
                    layout.Mappings.Select(mapping => mapping.PhysicalKeyId)
                        .Order(StringComparer.Ordinal)
                        .ToArray()))
                .ToArray(),
            files.Select(file => new BundleFile(file.RelativePath, file.Sha256)).ToArray());

        return JsonSerializer.Serialize(manifest, ManifestOptions) + "\n";
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private sealed record BundleManifest(
        int SchemaVersion,
        string GeneratorVersion,
        IReadOnlyList<BundleVariant> Variants,
        IReadOnlyList<BundleFile> Files);

    private sealed record BundleVariant(
        string ProjectInstallationId,
        string BaseLayoutId,
        string? BaseVariantId,
        string ResolvedBaseSectionId,
        string PublicVariantId,
        string InternalSectionId,
        string Description,
        IReadOnlyList<string> ChangedKeyIds);

    private sealed record BundleFile(string RelativePath, string Sha256);
}
