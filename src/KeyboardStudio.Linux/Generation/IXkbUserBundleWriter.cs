namespace KeyboardStudio.Linux;

public interface IXkbUserBundleWriter
{
    Task<XkbUserBundleWriteResult> WriteAsync(
        XkbGeneratedUserBundle bundle,
        string outputRoot,
        CancellationToken cancellationToken = default);
}
