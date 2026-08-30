using KeyboardStudio.Linux;

namespace KeyboardStudio.App;

public sealed record LinuxUserVariantOperationResult(
    bool Success,
    string Message,
    string? OutputPath,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
