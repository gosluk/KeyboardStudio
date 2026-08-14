namespace KeyboardStudio.Build;

public interface IArtifactVerifier
{
    Task<ArtifactVerificationResult> VerifyAsync(
        string artifactPath,
        BuildTarget target,
        CancellationToken cancellationToken = default);
}
