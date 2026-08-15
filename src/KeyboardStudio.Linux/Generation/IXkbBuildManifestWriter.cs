namespace KeyboardStudio.Linux;

public interface IXkbBuildManifestWriter
{
    Task<string> WriteAsync(
        XkbBuildManifest manifest,
        string outputRoot,
        CancellationToken cancellationToken = default);
}
