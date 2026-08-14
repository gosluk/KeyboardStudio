namespace KeyboardStudio.Build;

public sealed record BuildVerificationManifest(
    string? Machine,
    bool IsDll,
    bool ExpectedExportFound,
    ArtifactLoadTestStatus LoadTestStatus);
