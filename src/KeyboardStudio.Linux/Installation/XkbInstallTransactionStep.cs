namespace KeyboardStudio.Linux;

public enum XkbInstallTransactionStep
{
    ProposedRootPrepared,
    ProposedRootVerified,
    BackupsPrepared,
    JournalWritten,
    DestinationApplied,
    InstalledRootVerified,
    ManifestWritten,
    JournalCleared,
    RollbackCompleted,
    RecoveryCompleted
}
