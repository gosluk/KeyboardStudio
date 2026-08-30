using System.Text.Json;

namespace KeyboardStudio.Linux;

public static class XkbInstallationManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Serialize(XkbInstallationManifest manifest)
    {
        Validate(manifest);
        return JsonSerializer.Serialize(manifest, Options) + "\n";
    }

    public static XkbInstallationManifest Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            var manifest = JsonSerializer.Deserialize<XkbInstallationManifest>(json, Options)
                ?? throw new InvalidDataException("The XKB installation manifest is empty.");
            Validate(manifest);
            return manifest;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The XKB installation manifest is malformed.", exception);
        }
    }

    private static void Validate(XkbInstallationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != XkbInstallationManifest.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"XKB installation manifest schema {manifest.SchemaVersion} is not supported.");
        }

        ArgumentNullException.ThrowIfNull(manifest.Installations);
        ArgumentNullException.ThrowIfNull(manifest.Files);
        foreach (var installation in manifest.Installations)
        {
            ArgumentNullException.ThrowIfNull(installation);
            if (installation.ProjectInstallationId.Length != 32 ||
                installation.ProjectInstallationId.Any(character => !char.IsAsciiHexDigit(character)) ||
                string.IsNullOrWhiteSpace(installation.BaseLayoutId) ||
                string.IsNullOrWhiteSpace(installation.ResolvedBaseSectionId) ||
                string.IsNullOrWhiteSpace(installation.PublicVariantId) ||
                string.IsNullOrWhiteSpace(installation.InternalSectionId) ||
                !IsSha256(installation.CentralBlockSha256) ||
                !IsSha256(installation.BridgeBlockSha256) ||
                !IsSha256(installation.RegistryEntrySha256))
            {
                throw new InvalidDataException("An installed variant has invalid identity or ownership hashes.");
            }
        }

        foreach (var file in manifest.Files)
        {
            ArgumentNullException.ThrowIfNull(file);
            var relativePath = file.RelativePath;
            var segments = relativePath?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath) ||
                relativePath.Contains('\\', StringComparison.Ordinal) ||
                segments.Any(segment => segment is "." or "..") ||
                !IsSha256(file.Sha256))
            {
                throw new InvalidDataException("A managed file record has an invalid path or hash.");
            }
        }

        if (manifest.Installations.Select(item => item.ProjectInstallationId)
            .Distinct(StringComparer.Ordinal).Count() != manifest.Installations.Count)
        {
            throw new InvalidDataException("An installation ID occurs more than once in the manifest.");
        }

        if (manifest.Installations.Select(item => (item.BaseLayoutId, item.PublicVariantId))
            .Distinct().Count() != manifest.Installations.Count)
        {
            throw new InvalidDataException("A public layout/variant pair occurs more than once in the manifest.");
        }

        if (manifest.Files.Select(file => file.RelativePath)
            .Distinct(StringComparer.Ordinal).Count() != manifest.Files.Count)
        {
            throw new InvalidDataException("A managed file path occurs more than once in the manifest.");
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
