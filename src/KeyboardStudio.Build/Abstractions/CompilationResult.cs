namespace KeyboardStudio.Build;

public sealed record CompilationResult(
    bool Success,
    string? ArtifactPath,
    IReadOnlyList<CompilerMessage> Messages,
    string RawLog = "",
    string? LogPath = null,
    string? WorkspacePath = null,
    ArtifactVerificationResult? Verification = null);
