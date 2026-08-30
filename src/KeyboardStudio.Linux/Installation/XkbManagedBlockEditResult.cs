namespace KeyboardStudio.Linux;

public sealed record XkbManagedBlockEditResult(
    bool Success,
    string? Content,
    string? ManagedBlockSha256,
    bool Changed,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
