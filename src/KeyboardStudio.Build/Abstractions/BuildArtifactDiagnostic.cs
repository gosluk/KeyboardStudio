namespace KeyboardStudio.Build;

public sealed record BuildArtifactDiagnostic(
    BuildDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? KeyId = null);
