namespace KeyboardStudio.App;

/// <summary>
/// Runs the ordered startup steps that must happen before the first window exists.
/// </summary>
/// <remarks>
/// The saved appearance has to be applied before any window is constructed, otherwise the first
/// frame renders in the Fluent default and is then corrected in front of the user. Expressing that
/// order as a type rather than as a comment in the composition root is what makes it testable.
/// </remarks>
public sealed class ApplicationStartupSequence
{
    private readonly IApplicationSettingsStore _settingsStore;
    private readonly IApplicationThemeService _themeService;

    public ApplicationStartupSequence(
        IApplicationSettingsStore settingsStore,
        IApplicationThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(themeService);

        _settingsStore = settingsStore;
        _themeService = themeService;
    }

    /// <summary>
    /// Restores the saved appearance and only then builds the application shell.
    /// </summary>
    public T Start<T>(Func<T> createShell)
    {
        ArgumentNullException.ThrowIfNull(createShell);

        RestoreAppearance();
        return createShell();
    }

    /// <summary>
    /// Loads the saved preferences and applies the theme they name, falling back to the defaults
    /// when they cannot be read.
    /// </summary>
    /// <returns>
    /// The load result, so a caller can surface a failure without the failure having blocked
    /// startup.
    /// </returns>
    public ApplicationSettingsLoadResult RestoreAppearance()
    {
        // Deliberately synchronous: there is nothing useful to draw before the theme is known, and
        // the read is one small file in the local application-data directory. Task.Run keeps the
        // continuation off the UI thread, so waiting here cannot deadlock against it.
        var result = Task.Run(() => _settingsStore.LoadAsync()).GetAwaiter().GetResult();
        _themeService.Apply(result.Settings.Theme);
        return result;
    }
}
