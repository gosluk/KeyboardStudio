namespace KeyboardStudio.Linux;

public sealed record XkbInstallFileSnapshot(
    string RelativePath,
    string? Content,
    bool IsSymbolicLink)
{
    public bool Exists => Content is not null;
}
