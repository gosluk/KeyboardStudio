using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class AppearanceViewModelTests
{
    [Theory]
    [InlineData(ApplicationTheme.White)]
    [InlineData(ApplicationTheme.Black)]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_WhenAThemeIsChosen_AppliesItImmediatelyAndSavesItOnce(ApplicationTheme theme)
    {
        var (viewModel, store, themeService) = Create();

        await viewModel.SelectAsync(theme);

        Assert.Equal(theme, themeService.CurrentTheme);
        Assert.Equal(theme, viewModel.SelectedTheme);
        Assert.Equal([theme], store.Saved.Select(settings => settings.Theme));
        Assert.Equal(
            [ApplicationSettings.CurrentSchemaVersion],
            store.Saved.Select(settings => settings.SchemaVersion));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_WhenEachThemeIsChosenInTurn_SavesOncePerChange()
    {
        var (viewModel, store, _) = Create();

        await viewModel.SelectAsync(ApplicationTheme.White);
        await viewModel.SelectAsync(ApplicationTheme.Black);
        await viewModel.SelectAsync(ApplicationTheme.Gray);

        Assert.Equal(
            [ApplicationTheme.White, ApplicationTheme.Black, ApplicationTheme.Gray],
            store.Saved.Select(settings => settings.Theme));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_WhenTheThemeIsAlreadyActive_SavesNothing()
    {
        var (viewModel, store, _) = Create();

        await viewModel.SelectAsync(ApplicationTheme.Gray);
        await viewModel.SelectAsync(ApplicationTheme.Gray);

        Assert.Empty(store.Saved);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task SelectAsync_WhenSavingFails_KeepsTheThemeAndWarnsWithoutBlocking()
    {
        var (viewModel, store, themeService) = Create();
        store.FailWith = new ApplicationSettingsError(ApplicationSettingsErrorKind.AccessDenied, "denied");

        await viewModel.SelectAsync(ApplicationTheme.Black);

        Assert.Equal(ApplicationTheme.Black, themeService.CurrentTheme);
        Assert.Equal(ApplicationTheme.Black, viewModel.SelectedTheme);
        Assert.True(viewModel.HasWarning);
        Assert.NotNull(viewModel.Warning);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.Options.Single(option => option.Theme == ApplicationTheme.Black).IsSelected);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task SelectAsync_WhenALaterSaveSucceeds_ClearsTheWarning()
    {
        var (viewModel, store, _) = Create();
        store.FailWith = new ApplicationSettingsError(ApplicationSettingsErrorKind.Io, "interrupted");
        await viewModel.SelectAsync(ApplicationTheme.Black);
        Assert.True(viewModel.HasWarning);

        store.FailWith = null;
        await viewModel.SelectAsync(ApplicationTheme.White);

        Assert.False(viewModel.HasWarning);
        Assert.Null(viewModel.Warning);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Options_OfferEveryThemeWithAName_AndFollowTheActiveOne()
    {
        var themeService = new RecordingApplicationThemeService();
        themeService.Apply(ApplicationTheme.Black);
        var viewModel = new AppearanceViewModel(new CountingApplicationSettingsStore(), themeService);

        Assert.Equal(
            [ApplicationTheme.White, ApplicationTheme.Gray, ApplicationTheme.Black],
            viewModel.Options.Select(option => option.Theme));
        Assert.All(viewModel.Options, option => Assert.False(string.IsNullOrWhiteSpace(option.Name)));
        Assert.All(viewModel.Options, option => Assert.False(string.IsNullOrWhiteSpace(option.Description)));
        Assert.Equal(
            [ApplicationTheme.Black],
            viewModel.Options.Where(option => option.IsSelected).Select(option => option.Theme));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Options_WhenAThemeIsSelected_LeaveExactlyOneChecked()
    {
        var (viewModel, _, _) = Create();

        await viewModel.SelectAsync(ApplicationTheme.White);

        Assert.Equal(
            [ApplicationTheme.White],
            viewModel.Options.Where(option => option.IsSelected).Select(option => option.Theme));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Option_WhenCheckedByTheRadioGroup_SelectsThatTheme()
    {
        var (viewModel, store, themeService) = Create();

        // What the radio button does: the group clears the previous option and checks the new one.
        viewModel.Options.Single(option => option.Theme == ApplicationTheme.Gray).IsSelected = false;
        viewModel.Options.Single(option => option.Theme == ApplicationTheme.Black).IsSelected = true;

        await WaitForSave(store);

        Assert.Equal(ApplicationTheme.Black, themeService.CurrentTheme);
        Assert.Equal([ApplicationTheme.Black], store.Saved.Select(settings => settings.Theme));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultConstruction_TouchesNeitherHostStorageNorAnApplication()
    {
        var viewModel = new AppearanceViewModel();

        Assert.Equal(ApplicationTheme.Gray, viewModel.SelectedTheme);
        Assert.False(viewModel.HasWarning);
        Assert.Equal(3, viewModel.Options.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MainWindowViewModel_ExposesAppearanceWithoutTouchingTheDocument()
    {
        var viewModel = new MainWindowViewModel();
        var project = viewModel.Project;

        Assert.NotNull(viewModel.Appearance);
        Assert.Equal(3, viewModel.Appearance.Options.Count);
        Assert.Same(project, viewModel.Project);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Constructor_WhenACollaboratorIsMissing_Rejects()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AppearanceViewModel(null!, new RecordingApplicationThemeService()));
        Assert.Throws<ArgumentNullException>(
            () => new AppearanceViewModel(new CountingApplicationSettingsStore(), null!));
    }

    private static async Task WaitForSave(CountingApplicationSettingsStore store)
    {
        for (var attempt = 0; attempt < 100 && store.Saved.Count == 0; attempt++)
        {
            await Task.Yield();
        }
    }

    private static (AppearanceViewModel ViewModel,
        CountingApplicationSettingsStore Store,
        RecordingApplicationThemeService ThemeService) Create()
    {
        var store = new CountingApplicationSettingsStore();
        var themeService = new RecordingApplicationThemeService();
        return (new AppearanceViewModel(store, themeService), store, themeService);
    }
}
