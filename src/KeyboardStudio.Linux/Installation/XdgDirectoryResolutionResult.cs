namespace KeyboardStudio.Linux;

public sealed record XdgDirectoryResolutionResult(
    bool Success,
    XdgDirectoryPaths? Paths,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
