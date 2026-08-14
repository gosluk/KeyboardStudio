namespace KeyboardStudio.Build;

public sealed record BuildReproducibilityResult(
    bool Success,
    bool GeneratedSourcesMatch,
    bool BinaryOutputsMatch,
    string? FirstArtifactSha256,
    string? SecondArtifactSha256,
    string? ComparisonWorkspacePath,
    IReadOnlyList<CompilerMessage> Messages);
