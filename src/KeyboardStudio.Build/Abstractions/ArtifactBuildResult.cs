namespace KeyboardStudio.Build;

public sealed record ArtifactBuildResult(
    bool Success,
    string? ArtifactPath,
    IReadOnlyList<BuildArtifactDiagnostic> Diagnostics,
    string RawLog = "",
    string? LogPath = null,
    string? WorkspacePath = null,
    string? ManifestPath = null,
    string? ArtifactSha256 = null,
    object? BackendDetails = null);
