using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyboardStudio.App;

/// <summary>
/// Presents the three appearance choices and commits the one the user picks.
/// </summary>
/// <remarks>
/// The theme is applied first and saved second. Appearance is worth nothing if it takes a
/// round-trip to disk to appear, and a preference that cannot be written is still a preference the
/// user made: a failed save keeps the chosen theme for the session and says so beside the choice,
/// rather than reverting the window under them or interrupting with a dialog.
/// </remarks>
public sealed class AppearanceViewModel : ObservableObject
{
    private readonly IApplicationSettingsStore _settingsStore;
    private readonly IApplicationThemeService _themeService;
    private bool _isBusy;
    private string? _warning;

    public AppearanceViewModel()
        : this(new NoOpApplicationSettingsStore(), new NoOpApplicationThemeService())
    {
    }

    public AppearanceViewModel(
        IApplicationSettingsStore settingsStore,
        IApplicationThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(themeService);

        _settingsStore = settingsStore;
        _themeService = themeService;

        Options =
        [
            Option(ApplicationTheme.White, "White", "Bright workspace"),
            Option(ApplicationTheme.Gray, "Gray", "Cool neutral workspace"),
            Option(ApplicationTheme.Black, "Black", "Near-black workspace"),
        ];

        SyncSelection();
    }

    public IReadOnlyList<ThemeOptionViewModel> Options { get; }

    public ApplicationTheme SelectedTheme => _themeService.CurrentTheme;

    /// <summary>True while a chosen theme is being written to the settings file.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>A non-modal explanation of a preference that could not be saved.</summary>
    public string? Warning
    {
        get => _warning;
        private set
        {
            if (SetProperty(ref _warning, value))
            {
                OnPropertyChanged(nameof(HasWarning));
            }
        }
    }

    public bool HasWarning => !string.IsNullOrEmpty(Warning);

    /// <summary>
    /// Applies <paramref name="theme"/> everywhere and persists it. Choosing the theme that is
    /// already active does nothing, so reopening the menu does not rewrite the settings file.
    /// </summary>
    public async Task SelectAsync(ApplicationTheme theme, CancellationToken cancellationToken = default)
    {
        if (theme == SelectedTheme)
        {
            SyncSelection();
            return;
        }

        _themeService.Apply(theme);
        SyncSelection();
        OnPropertyChanged(nameof(SelectedTheme));

        IsBusy = true;
        try
        {
            var result = await _settingsStore.SaveAsync(
                new ApplicationSettings(ApplicationSettings.CurrentSchemaVersion, theme),
                cancellationToken);

            Warning = result.Success
                ? null
                : "This appearance is active for now, but it could not be saved for next time.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ThemeOptionViewModel Option(ApplicationTheme theme, string name, string description) =>
        new(theme, name, description, option => _ = SelectAsync(option.Theme));

    private void SyncSelection()
    {
        var selected = SelectedTheme;
        foreach (var option in Options)
        {
            option.SetSelectedWithoutNotifying(option.Theme == selected);
        }
    }
}
