namespace KeyboardStudio.Linux;

public sealed record XkbUserBundleVerificationResult(
    XkbUserBundleVerificationStatus Status,
    string? ToolPath,
    string? ToolVersion,
    IReadOnlyList<XkbUserBundleVerificationCheck> Checks,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
