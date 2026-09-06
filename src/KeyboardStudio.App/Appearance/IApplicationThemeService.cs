namespace KeyboardStudio.App;

/// <summary>
/// Applies a neutral appearance choice to the whole application.
/// </summary>
/// <remarks>
/// ViewModels state which theme they want and never touch Avalonia theme variants, so appearance
/// behaviour stays testable without constructing an application lifetime.
/// </remarks>
public interface IApplicationThemeService
{
    /// <summary>The theme currently presented by the application.</summary>
    ApplicationTheme CurrentTheme { get; }

    /// <summary>
    /// Presents <paramref name="theme"/> across every application surface. Applying the theme that
    /// is already current changes nothing.
    /// </summary>
    void Apply(ApplicationTheme theme);
}
