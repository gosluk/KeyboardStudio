namespace KeyboardStudio.Linux;

public sealed record XkbTransactionFileBackup(
    string RelativePath,
    bool Existed,
    string? Sha256);
