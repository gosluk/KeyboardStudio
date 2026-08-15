namespace KeyboardStudio.Build;

public static class BuildStageNames
{
    public const string Validating = "Validating";
    public const string Generating = "Generating";
    public const string GeneratingXkb = "Generating XKB";
    public const string Compiling = "Compiling";
    public const string Linking = "Linking";
    public const string WritingArtifact = "Writing artifact";
    public const string Verifying = "Verifying";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}
