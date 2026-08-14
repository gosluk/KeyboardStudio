namespace KeyboardStudio.Linux;

public sealed record XkbBuildDetails(
    XkbBuildManifest Manifest,
    string ManifestPath,
    XkbGeneratedSymbols GeneratedSymbols);
