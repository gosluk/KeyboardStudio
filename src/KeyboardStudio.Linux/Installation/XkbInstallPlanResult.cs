namespace KeyboardStudio.Linux;

public sealed record XkbInstallPlanResult(
    bool Success,
    XkbInstallPlan? Plan,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
