namespace KeyboardStudio.Build;

public interface IArtifactLoadTester
{
    Task<ArtifactLoadTestResult> TestAsync(
        string artifactPath,
        BuildTarget target,
        string exportName,
        CancellationToken cancellationToken = default);
}
