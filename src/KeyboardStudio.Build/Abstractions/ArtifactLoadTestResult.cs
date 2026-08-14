namespace KeyboardStudio.Build;

public sealed record ArtifactLoadTestResult(
    ArtifactLoadTestStatus Status,
    string Message);
