namespace KeyboardStudio.Build;

public sealed record BuildManifestWriteResult(
    BuildManifest Manifest,
    string ManifestPath);
