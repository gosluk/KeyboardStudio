namespace KeyboardStudio.App.Tests;

/// <summary>
/// Records every save and can be told to fail them.
/// </summary>
internal sealed class CountingApplicationSettingsStore : IApplicationSettingsStore
{
    private readonly List<ApplicationSettings> _saved = [];

    public IReadOnlyList<ApplicationSettings> Saved => _saved;

    public ApplicationSettingsError? FailWith { get; set; }

    public Task<ApplicationSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApplicationSettingsLoadResult.Loaded(ApplicationSettings.Default));

    public Task<ApplicationSettingsSaveResult> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        _saved.Add(settings);
        return Task.FromResult(FailWith is null
            ? ApplicationSettingsSaveResult.Saved()
            : ApplicationSettingsSaveResult.Failed(FailWith));
    }
}
