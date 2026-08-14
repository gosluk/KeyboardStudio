namespace KeyboardStudio.Build;

public sealed record BuildEnvironmentStatus(
    bool Available,
    string Message,
    IReadOnlyList<BuildEnvironmentDiagnostic> Diagnostics,
    IReadOnlyList<BuildTarget> SupportedTargets);
