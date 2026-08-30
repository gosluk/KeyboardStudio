namespace KeyboardStudio.Linux;

public sealed record XkbUserRecoveryResult(
    bool Success,
    bool Recovered,
    string? TransactionId,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
