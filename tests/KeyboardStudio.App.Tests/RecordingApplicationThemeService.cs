namespace KeyboardStudio.App.Tests;

/// <summary>
/// Records the themes it was asked to apply, in order.
/// </summary>
internal sealed class RecordingApplicationThemeService : IApplicationThemeService
{
    private readonly List<ApplicationTheme> _applied = [];

    public IReadOnlyList<ApplicationTheme> Applied => _applied;

    public ApplicationTheme CurrentTheme { get; private set; } = ApplicationSettings.Default.Theme;

    public void Apply(ApplicationTheme theme)
    {
        _applied.Add(theme);
        CurrentTheme = theme;
    }
}
