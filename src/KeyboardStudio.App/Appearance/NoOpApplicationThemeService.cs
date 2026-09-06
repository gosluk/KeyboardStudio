namespace KeyboardStudio.App;

/// <summary>
/// Remembers a theme choice without presenting it.
/// </summary>
/// <remarks>
/// Used where appearance is composed without an Avalonia application to apply it to — the
/// design-time and default view-model paths — so nothing has to special-case a missing service.
/// </remarks>
public sealed class NoOpApplicationThemeService : IApplicationThemeService
{
    public ApplicationTheme CurrentTheme { get; private set; } = ApplicationSettings.Default.Theme;

    public void Apply(ApplicationTheme theme) => CurrentTheme = theme;
}
