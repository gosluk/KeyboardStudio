namespace KeyboardStudio.App;

public sealed class LocalApplicationSettingsPathProvider : IApplicationSettingsPathProvider
{
    private readonly string _localApplicationDataRoot;

    public LocalApplicationSettingsPathProvider()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public LocalApplicationSettingsPathProvider(string localApplicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataRoot);
        _localApplicationDataRoot = localApplicationDataRoot;
    }

    public string GetSettingsPath() =>
        Path.Combine(_localApplicationDataRoot, "KeyboardStudio", "settings.json");
}
