using Avalonia;

namespace KeyboardStudio.App;

/// <summary>
/// The one place that drives <see cref="Application.RequestedThemeVariant"/>.
/// </summary>
/// <remarks>
/// Keeping the Avalonia dependency here means the rest of the application — including the
/// appearance ViewModel — deals only in <see cref="ApplicationTheme"/>.
/// </remarks>
public sealed class AvaloniaApplicationThemeService : IApplicationThemeService
{
    private readonly Application _application;

    public AvaloniaApplicationThemeService(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
        CurrentTheme = ApplicationSettings.Default.Theme;
    }

    public ApplicationTheme CurrentTheme { get; private set; }

    public void Apply(ApplicationTheme theme)
    {
        var variant = ApplicationThemeVariants.For(theme);
        if (CurrentTheme == theme && Equals(_application.RequestedThemeVariant, variant))
        {
            return;
        }

        _application.RequestedThemeVariant = variant;
        CurrentTheme = theme;
    }
}
