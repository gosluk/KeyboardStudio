namespace KeyboardStudio.Linux;

public sealed record XkbUserBundleVerificationCheck(
    XkbUserBundleVerificationCheckKind Kind,
    string LayoutId,
    string? VariantId,
    bool Success,
    IReadOnlyList<string> Arguments,
    int? ExitCode,
    string StandardOutput,
    string StandardError);
