namespace KeyboardStudio.App;

public sealed record ApplicationSettings(int SchemaVersion, ApplicationTheme Theme)
{
    public const int CurrentSchemaVersion = 1;

    public static ApplicationSettings Default { get; } = new(CurrentSchemaVersion, ApplicationTheme.Gray);
}
