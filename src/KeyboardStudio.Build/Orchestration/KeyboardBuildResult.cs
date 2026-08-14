using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed record KeyboardBuildResult(
    bool Success,
    IReadOnlyList<ValidationIssue> ValidationIssues,
    ArtifactBuildResult? Artifact,
    BuildReproducibilityResult? Reproducibility = null)
{
    public CompilationResult? Compilation => Artifact?.BackendDetails as CompilationResult;
}
