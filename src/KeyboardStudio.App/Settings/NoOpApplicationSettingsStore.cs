namespace KeyboardStudio.App;

/// <summary>
/// Reports the default preferences and discards every change.
/// </summary>
/// <remarks>
/// Used where appearance is composed without host storage, so the default view-model path never
/// reads or writes the developer's real preference file.
/// </remarks>
public sealed class NoOpApplicationSettingsStore : IApplicationSettingsStore
{
    public Task<ApplicationSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApplicationSettingsLoadResult.Loaded(ApplicationSettings.Default));

    public Task<ApplicationSettingsSaveResult> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Task.FromResult(ApplicationSettingsSaveResult.Saved());
    }
}
