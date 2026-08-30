namespace KeyboardStudio.Linux;

/// <summary>Receives transaction milestones for diagnostics and deterministic failure testing.</summary>
public interface IXkbInstallTransactionObserver
{
    void OnStep(XkbInstallTransactionStep milestone, string transactionId, string? relativePath);
}
