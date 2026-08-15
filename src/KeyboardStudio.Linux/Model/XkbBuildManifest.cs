using KeyboardStudio.Build;

namespace KeyboardStudio.Linux;

public sealed record XkbBuildManifest(
    int SchemaVersion,
    string ProjectName,
    BuildTarget Target,
    string LayoutId,
    string SectionId,
    string GeneratorVersion,
    string ArtifactPath,
    string ArtifactSha256,
    string VerificationStatus,
    string? VerifierVersion,
    DateTimeOffset BuildTimestampUtc);
