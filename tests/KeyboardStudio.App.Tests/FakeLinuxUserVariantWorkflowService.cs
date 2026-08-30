using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App.Tests;

internal sealed class FakeLinuxUserVariantWorkflowService : ILinuxUserVariantWorkflowService
{
    private readonly Queue<LinuxUserVariantPreparation> _inspections = [];

    public int InspectCount { get; private set; }

    public int GenerateCount { get; private set; }

    public int InstallOrUpdateCount { get; private set; }

    public int VerifyCount { get; private set; }

    public int UninstallCount { get; private set; }

    public string? LastVariantId { get; private set; }

    public string? LastDisplayName { get; private set; }

    public bool WaitForCancellation { get; set; }

    public LinuxUserVariantOperationResult OperationResult { get; set; } =
        new(true, "Operation succeeded.", null, []);

    public FakeLinuxUserVariantWorkflowService AddInspection(LinuxUserVariantPreparation preparation)
    {
        _inspections.Enqueue(preparation);
        return this;
    }

    public Task<LinuxUserVariantPreparation> InspectAsync(
        KeyboardProject project,
        LayoutDerivation? derivation,
        string? publicVariantId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        InspectCount++;
        LastVariantId = publicVariantId;
        LastDisplayName = displayName;
        cancellationToken.ThrowIfCancellationRequested();
        if (_inspections.Count == 0)
        {
            throw new InvalidOperationException("No fake Linux user-variant inspection was configured.");
        }

        return Task.FromResult(_inspections.Count > 1
            ? _inspections.Dequeue()
            : _inspections.Peek());
    }

    public async Task<LinuxUserVariantOperationResult> GenerateAsync(
        LinuxUserVariantPreparation preparation,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        GenerateCount++;
        return await CompleteAsync(cancellationToken);
    }

    public async Task<LinuxUserVariantOperationResult> InstallOrUpdateAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        InstallOrUpdateCount++;
        return await CompleteAsync(cancellationToken);
    }

    public async Task<LinuxUserVariantOperationResult> VerifyInstalledAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        VerifyCount++;
        return await CompleteAsync(cancellationToken);
    }

    public async Task<LinuxUserVariantOperationResult> UninstallAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        UninstallCount++;
        return await CompleteAsync(cancellationToken);
    }

    private async Task<LinuxUserVariantOperationResult> CompleteAsync(
        CancellationToken cancellationToken)
    {
        if (WaitForCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        return OperationResult;
    }
}
