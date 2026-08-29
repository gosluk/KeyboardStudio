namespace KeyboardStudio.Linux;

public sealed record XkbUserBundleWriteResult(
    string BundleRoot,
    IReadOnlyList<string> WrittenPaths);
