namespace KeyboardStudio.App;

public readonly record struct ApplicationSettingsSaveResult(bool Success, ApplicationSettingsError? Error)
{
    public static ApplicationSettingsSaveResult Saved() => new(true, null);

    public static ApplicationSettingsSaveResult Failed(ApplicationSettingsError error) => new(false, error);
}
