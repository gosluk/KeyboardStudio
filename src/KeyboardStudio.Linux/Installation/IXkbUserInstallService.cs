namespace KeyboardStudio.Linux;

public interface IXkbUserInstallService
{
    Task<XkbUserInstallResult> InstallOrUpdateAsync(
        XkbGeneratedUserBundle bundle,
        XkbUserVariantMetadata metadata,
        XdgDirectoryPaths paths,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken = default);

    Task<XkbUserInstallResult> VerifyInstalledAsync(
        string projectInstallationId,
        XdgDirectoryPaths paths,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken = default);

    Task<XkbUserInstallResult> UninstallAsync(
        string projectInstallationId,
        XdgDirectoryPaths paths,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken = default);

    Task<XkbUserRecoveryResult> RecoverAsync(
        XdgDirectoryPaths paths,
        CancellationToken cancellationToken = default);
}
