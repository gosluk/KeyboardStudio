using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

public interface ILinuxUserVariantWorkflowService
{
    Task<LinuxUserVariantPreparation> InspectAsync(
        KeyboardProject project,
        LayoutDerivation? derivation,
        string? publicVariantId,
        string? displayName,
        CancellationToken cancellationToken = default);

    Task<LinuxUserVariantOperationResult> GenerateAsync(
        LinuxUserVariantPreparation preparation,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    Task<LinuxUserVariantOperationResult> InstallOrUpdateAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default);

    Task<LinuxUserVariantOperationResult> VerifyInstalledAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default);

    Task<LinuxUserVariantOperationResult> UninstallAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default);
}
