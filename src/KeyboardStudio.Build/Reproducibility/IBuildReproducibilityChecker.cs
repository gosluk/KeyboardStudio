namespace KeyboardStudio.Build;

public interface IBuildReproducibilityChecker
{
    Task<BuildReproducibilityResult> CompareAsync(
        GeneratedArtifact firstGeneratedArtifact,
        string firstArtifactPath,
        GeneratedArtifact secondGeneratedArtifact,
        string secondArtifactPath,
        string? comparisonWorkspacePath,
        CancellationToken cancellationToken = default);
}
