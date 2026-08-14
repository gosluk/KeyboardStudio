using KeyboardStudio.Build;

namespace KeyboardStudio.Linux;

public sealed record XkbVerificationResult(
    XkbVerificationStatus Status,
    bool ManagedValidationPassed,
    string? ToolPath,
    string? ToolVersion,
    IReadOnlyList<string> Arguments,
    string StandardOutput,
    string StandardError,
    int? ExitCode,
    TimeSpan Duration,
    string? LogPath,
    IReadOnlyList<BuildArtifactDiagnostic> Diagnostics);
