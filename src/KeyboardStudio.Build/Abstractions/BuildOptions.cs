namespace KeyboardStudio.Build;

public sealed record BuildOptions(
    BuildTarget Target,
    string OutputDirectory,
    BuildCleanupPolicy CleanupPolicy = BuildCleanupPolicy.KeepFailedBuild);
