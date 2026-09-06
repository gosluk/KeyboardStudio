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
    /// <summary>
    /// Keys this bundle writes incompletely, and what each one costs. Empty for a variant that
    /// carries everything.
    /// </summary>
    /// <remarks>
    /// The bundle already contains them, because knowing whether they can be written at all means
    /// writing them. Nothing may reach the filesystem until the user has been shown this list and
    /// agreed to it — <see cref="LinuxUserVariantViewModel"/> is what asks, and it asks before
    /// every generate, install, and update.
    /// </remarks>
    public IReadOnlyList<XkbDiagnostic> AcceptedLoss { get; init; } = [];

    public bool CanGenerate => Bundle is not null;

    public bool CanManage => Bundle is not null &&
                             Paths is not null &&
                             Capability?.Mode == XkbUserInstallMode.ManagedInstallation;

    public bool IsInstalled => Status is LinuxUserVariantStatus.Installed or
        LinuxUserVariantStatus.UpdateAvailable or
        LinuxUserVariantStatus.ExternallyModified or
        LinuxUserVariantStatus.Broken;
}
