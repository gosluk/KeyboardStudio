using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App.Tests;

internal sealed class SilentLinuxUserVariantWorkflowService : ILinuxUserVariantWorkflowService
{
    public Task<LinuxUserVariantPreparation> InspectAsync(
        KeyboardProject project,
        LayoutDerivation? derivation,
        string? publicVariantId,
        string? displayName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new LinuxUserVariantPreparation(
            LinuxUserVariantStatus.Unavailable,
            null,
            null,
            null,
            null,
            null,
            [new XkbDiagnostic("TEST", "Host Linux workflow is disabled in this test.")]));

    public Task<LinuxUserVariantOperationResult> GenerateAsync(
        LinuxUserVariantPreparation preparation,
        string outputDirectory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Failed());

    public Task<LinuxUserVariantOperationResult> InstallOrUpdateAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Failed());

    public Task<LinuxUserVariantOperationResult> VerifyInstalledAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Failed());

    public Task<LinuxUserVariantOperationResult> UninstallAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Failed());

    private static LinuxUserVariantOperationResult Failed() =>
        new(false, "Host Linux workflow is disabled in this test.", null, []);
}
