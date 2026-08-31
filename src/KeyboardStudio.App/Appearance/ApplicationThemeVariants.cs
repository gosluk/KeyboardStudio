using Avalonia.Styling;

namespace KeyboardStudio.App;

/// <summary>
/// The Avalonia theme variants that carry KeyboardStudio's White, Gray, and Black palettes.
/// </summary>
/// <remarks>
/// Each variant names an inherited Fluent variant. The inherited variant supplies complete control
/// resources for everything KeyboardStudio does not define itself, so a missing token degrades to a
/// readable Fluent colour rather than to nothing. White and Gray are light-control themes; only
/// Black inherits dark control semantics, including supported native window decoration.
/// </remarks>
public static class ApplicationThemeVariants
{
    /// <summary>The bright workspace variant.</summary>
    public static ThemeVariant White { get; } = new(nameof(White), ThemeVariant.Light);

    /// <summary>The cool neutral variant, and the product default.</summary>
    public static ThemeVariant Gray { get; } = new(nameof(Gray), ThemeVariant.Light);

    /// <summary>The near-black variant.</summary>
    public static ThemeVariant Black { get; } = new(nameof(Black), ThemeVariant.Dark);

    /// <summary>
    /// Translates a stored neutral preference into the Avalonia variant that presents it.
    /// </summary>
    public static ThemeVariant For(ApplicationTheme theme) => theme switch
    {
        ApplicationTheme.White => White,
        ApplicationTheme.Gray => Gray,
        ApplicationTheme.Black => Black,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unknown application theme."),
    };
}
