namespace KeyboardStudio.App.Tests;

/// <summary>
/// Resolves a settings path chosen by the test rather than by the host.
/// </summary>
internal sealed class FixedApplicationSettingsPathProvider : IApplicationSettingsPathProvider
{
    private readonly string _path;

    public FixedApplicationSettingsPathProvider(string path) => _path = path;

    public string GetSettingsPath() => _path;
}
