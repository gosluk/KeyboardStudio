namespace KeyboardStudio.Linux;

public sealed record XkbKeyNameMappingResult(
    bool Success,
    string? KeyName,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
