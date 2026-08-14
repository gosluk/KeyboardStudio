namespace KeyboardStudio.Build;

public sealed record BuildReproducibilityManifest(
    bool Success,
    bool GeneratedSourcesMatch,
    bool BinaryOutputsMatch,
    string? FirstArtifactSha256,
    string? SecondArtifactSha256);
