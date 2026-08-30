namespace KeyboardStudio.Linux;

public sealed class NullXkbInstallTransactionObserver : IXkbInstallTransactionObserver
{
    public static NullXkbInstallTransactionObserver Instance { get; } = new();

    private NullXkbInstallTransactionObserver()
    {
    }

    public void OnStep(XkbInstallTransactionStep milestone, string transactionId, string? relativePath)
    {
    }
}
