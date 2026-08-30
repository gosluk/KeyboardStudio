namespace KeyboardStudio.Linux;

public sealed record XkbInstallationManifest(
    int SchemaVersion,
    IReadOnlyList<XkbInstalledVariant> Installations,
    IReadOnlyList<XkbManagedFileRecord> Files)
{
    public const int CurrentSchemaVersion = 1;

    public static XkbInstallationManifest Empty { get; } =
        new(CurrentSchemaVersion, [], []);
}
