namespace KeyboardStudio.Linux;

public interface IXkbUserInstallCapabilityProbe
{
    Task<XkbUserInstallCapability> ProbeAsync(CancellationToken cancellationToken = default);
}
