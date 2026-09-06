using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class ApplicationStartupSequenceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Start_AppliesTheSavedThemeBeforeTheShellIsCreated()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        File.WriteAllText(path, """{"schemaVersion": 1, "theme": "black"}""");
        var themeService = new RecordingApplicationThemeService();
        var sequence = CreateSequence(path, themeService);
        var themesAppliedWhenShellWasCreated = -1;

        var shell = sequence.Start(() =>
        {
            themesAppliedWhenShellWasCreated = themeService.Applied.Count;
            return new object();
        });

        Assert.NotNull(shell);
        Assert.Equal(1, themesAppliedWhenShellWasCreated);
        Assert.Equal([ApplicationTheme.Black], themeService.Applied);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Start_ReturnsTheShellTheFactoryCreated()
    {
        using var directory = new TemporarySettingsDirectory();
        var expected = new object();

        var shell = CreateSequence(directory.Combine("settings.json"), new RecordingApplicationThemeService())
            .Start(() => expected);

        Assert.Same(expected, shell);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RestoreAppearance_WhenNoPreferenceIsSaved_AppliesGray()
    {
        using var directory = new TemporarySettingsDirectory();
        var themeService = new RecordingApplicationThemeService();

        var result = CreateSequence(directory.Combine("settings.json"), themeService).RestoreAppearance();

        Assert.True(result.Success);
        Assert.Equal([ApplicationTheme.Gray], themeService.Applied);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void RestoreAppearance_WhenThePreferenceCannotBeRead_AppliesGrayAndReportsTheFailure()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        File.WriteAllText(path, "{ not json");
        var themeService = new RecordingApplicationThemeService();

        var result = CreateSequence(path, themeService).RestoreAppearance();

        Assert.False(result.Success);
        Assert.Equal(ApplicationSettingsErrorKind.InvalidData, result.Error?.Kind);
        Assert.Equal([ApplicationTheme.Gray], themeService.Applied);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Start_WhenThePreferenceCannotBeRead_StillCreatesTheShell()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        File.WriteAllText(path, """{"schemaVersion": 7, "theme": "black"}""");

        var shell = CreateSequence(path, new RecordingApplicationThemeService()).Start(() => new object());

        Assert.NotNull(shell);
    }

    [Theory]
    [InlineData("white", ApplicationTheme.White)]
    [InlineData("gray", ApplicationTheme.Gray)]
    [InlineData("black", ApplicationTheme.Black)]
    [Trait("Category", "Unit")]
    public void RestoreAppearance_WhenAPreferenceIsSaved_AppliesIt(
        string identifier,
        ApplicationTheme expected)
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        File.WriteAllText(path, $$"""{"schemaVersion": 1, "theme": "{{identifier}}"}""");
        var themeService = new RecordingApplicationThemeService();

        CreateSequence(path, themeService).RestoreAppearance();

        Assert.Equal([expected], themeService.Applied);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Constructor_WhenACollaboratorIsMissing_Rejects()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ApplicationStartupSequence(null!, new RecordingApplicationThemeService()));
        Assert.Throws<ArgumentNullException>(
            () => new ApplicationStartupSequence(
                new JsonApplicationSettingsStore(new FixedApplicationSettingsPathProvider("settings.json")),
                null!));
    }

    private static ApplicationStartupSequence CreateSequence(
        string settingsPath,
        IApplicationThemeService themeService) =>
        new(new JsonApplicationSettingsStore(new FixedApplicationSettingsPathProvider(settingsPath)), themeService);
}
