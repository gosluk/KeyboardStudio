namespace KeyboardStudio.App;

public readonly record struct ApplicationSettingsLoadResult(
    ApplicationSettings Settings,
    ApplicationSettingsError? Error)
{
    public bool Success => Error is null;

    public static ApplicationSettingsLoadResult Loaded(ApplicationSettings settings) => new(settings, null);

    public static ApplicationSettingsLoadResult Defaulted(ApplicationSettingsError error) =>
        new(ApplicationSettings.Default, error);
}
