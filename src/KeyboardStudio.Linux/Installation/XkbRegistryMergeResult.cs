namespace KeyboardStudio.Linux;

public sealed record XkbRegistryMergeResult(
    bool Success,
    string? Content,
    string? EntrySha256,
    bool Changed,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
