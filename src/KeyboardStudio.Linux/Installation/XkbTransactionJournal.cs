namespace KeyboardStudio.Linux;

public sealed record XkbTransactionJournal(
    int SchemaVersion,
    string TransactionId,
    XkbInstallAction Action,
    string ProjectInstallationId,
    string UserXkbRoot,
    string StateRoot,
    DateTimeOffset StartedAtUtc,
    IReadOnlyList<XkbTransactionFileBackup> Files,
    bool ManifestExisted,
    string? ManifestSha256)
{
    public const int CurrentSchemaVersion = 1;
}
