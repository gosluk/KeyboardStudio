namespace KeyboardStudio.Build;

public sealed record ArtifactVerificationResult(
    bool Success,
    BuildTarget Target,
    string? Machine,
    bool IsDll,
    bool ExpectedExportFound,
    IReadOnlyList<CompilerMessage> Messages);
