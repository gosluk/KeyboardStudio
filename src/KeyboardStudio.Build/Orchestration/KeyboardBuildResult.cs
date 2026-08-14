using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed record KeyboardBuildResult(
    bool Success,
    IReadOnlyList<ValidationIssue> ValidationIssues,
    CompilationResult? Compilation);
