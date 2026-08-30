namespace KeyboardStudio.Linux;

public sealed record XkbUserInstallResult(
    bool Success,
    XkbUserInstallCommand Command,
    XkbInstallationManifest? Manifest,
    XkbUserBundleVerificationResult? ProposedVerification,
    XkbUserBundleVerificationResult? InstalledVerification,
    bool RecoveredInterruptedTransaction,
    bool RolledBack,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
