namespace KeyboardStudio.Linux;

public sealed record XkbUserBundleGenerationResult(
    bool Success,
    XkbGeneratedUserBundle? Bundle,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
