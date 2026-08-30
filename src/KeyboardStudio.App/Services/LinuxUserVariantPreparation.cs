using KeyboardStudio.Linux;

namespace KeyboardStudio.App;

public sealed record LinuxUserVariantPreparation(
    LinuxUserVariantStatus Status,
    XkbUserVariantMetadata? Metadata,
    XkbGeneratedUserBundle? Bundle,
    XdgDirectoryPaths? Paths,
    XkbUserInstallCapability? Capability,
    XkbInstallationManifest? InstallationManifest,
    IReadOnlyList<XkbDiagnostic> Diagnostics)
{
    public bool CanGenerate => Bundle is not null;

    public bool CanManage => Bundle is not null &&
                             Paths is not null &&
                             Capability?.Mode == XkbUserInstallMode.ManagedInstallation;

    public bool IsInstalled => Status is LinuxUserVariantStatus.Installed or
        LinuxUserVariantStatus.UpdateAvailable or
        LinuxUserVariantStatus.ExternallyModified or
        LinuxUserVariantStatus.Broken;
}
