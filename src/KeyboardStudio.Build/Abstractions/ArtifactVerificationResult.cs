namespace KeyboardStudio.Build;

public sealed record ArtifactVerificationResult(
    bool Success,
    BuildTarget Target,
    string? Machine,
    bool IsDll,
    bool ExpectedExportFound,
    ArtifactLoadTestResult LoadTest,
    IReadOnlyList<CompilerMessage> Messages);
