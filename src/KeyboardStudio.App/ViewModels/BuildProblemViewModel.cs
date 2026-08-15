using KeyboardStudio.Build;

namespace KeyboardStudio.App;

public sealed record BuildProblemViewModel(
    BuildProblemKind Kind,
    string Category,
    BuildDiagnosticSeverity Severity,
    string Code,
    string Message);
