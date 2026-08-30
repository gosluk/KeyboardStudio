namespace KeyboardStudio.Linux;

public sealed record XkbGeneratedUserBundle(IReadOnlyList<XkbUserBundleFile> Files)
{
    public XkbUserBundleFile? Find(string relativePath) =>
        Files.FirstOrDefault(file => string.Equals(
            file.RelativePath,
            relativePath,
            StringComparison.Ordinal));
}
