namespace KeyboardStudio.Build;

public sealed record BuildManifest(
    int SchemaVersion,
    string ProjectName,
    BuildTarget Target,
    IReadOnlyList<BuildManifestFile> GeneratedSources,
    BuildToolchainVersions Toolchain,
    BuildManifestFile Output,
    BuildVerificationManifest Verification,
    DateTimeOffset BuildTimestampUtc);
