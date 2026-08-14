namespace KeyboardStudio.Build;

public sealed record CompilationResult(
    bool Success,
    string? ArtifactPath,
    IReadOnlyList<CompilerMessage> Messages,
    string RawLog = "",
    string? LogPath = null,
    string? WorkspacePath = null,
    ArtifactVerificationResult? Verification = null,
    BuildToolchainVersions? ToolchainVersions = null,
    BuildManifest? Manifest = null,
    string? ManifestPath = null,
    string? ArtifactSha256 = null);
