using Avalonia;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class AvaloniaApplicationThemeServiceTests
{
    [Theory]
    [InlineData(ApplicationTheme.White)]
    [InlineData(ApplicationTheme.Gray)]
    [InlineData(ApplicationTheme.Black)]
    [Trait("Category", "Unit")]
    public void Apply_WhenThemeIsSelected_RequestsTheMatchingVariant(ApplicationTheme theme)
    {
        var application = new Application();
        var service = new AvaloniaApplicationThemeService(application);

        service.Apply(theme);

        Assert.Equal(ApplicationThemeVariants.For(theme), application.RequestedThemeVariant);
        Assert.Equal(theme, service.CurrentTheme);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CurrentTheme_BeforeAnythingIsApplied_IsTheProductDefault() =>
        Assert.Equal(
            ApplicationTheme.Gray,
            new AvaloniaApplicationThemeService(new Application()).CurrentTheme);

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_WhenTheSameThemeIsAppliedTwice_ChangesNothingTheSecondTime()
    {
        var application = new Application();
        var service = new AvaloniaApplicationThemeService(application);
        service.Apply(ApplicationTheme.Black);

        var changes = 0;
        application.PropertyChanged += (_, args) =>
        {
            if (args.Property == Application.RequestedThemeVariantProperty)
            {
                changes++;
            }
        };

        service.Apply(ApplicationTheme.Black);

        Assert.Equal(0, changes);
        Assert.Equal(ApplicationTheme.Black, service.CurrentTheme);
        Assert.Equal(ApplicationThemeVariants.For(ApplicationTheme.Black), application.RequestedThemeVariant);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_WhenTheThemeChanges_ReplacesTheVariantEachTime()
    {
        var application = new Application();
        var service = new AvaloniaApplicationThemeService(application);

        service.Apply(ApplicationTheme.White);
        Assert.Equal(ApplicationThemeVariants.For(ApplicationTheme.White), application.RequestedThemeVariant);

        service.Apply(ApplicationTheme.Black);
        Assert.Equal(ApplicationThemeVariants.For(ApplicationTheme.Black), application.RequestedThemeVariant);

        service.Apply(ApplicationTheme.Gray);
        Assert.Equal(ApplicationThemeVariants.For(ApplicationTheme.Gray), application.RequestedThemeVariant);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_WhenGrayIsRestoredExplicitly_StillRequestsTheGrayVariant()
    {
        // Gray is the value CurrentTheme starts at, so applying it must not be mistaken for a
        // no-op: without this the application would keep Avalonia's default variant and follow the
        // operating-system theme.
        var application = new Application();
        var service = new AvaloniaApplicationThemeService(application);

        service.Apply(ApplicationTheme.Gray);

        Assert.Equal(ApplicationThemeVariants.For(ApplicationTheme.Gray), application.RequestedThemeVariant);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_WhenThemeChanges_LeavesTheOpenDocumentAndViewModelsAlone()
    {
        var application = new Application();
        var service = new AvaloniaApplicationThemeService(application);
        var viewModel = new MainWindowViewModel();
        var project = viewModel.Project;
        var editor = viewModel.Editor;
        Assert.True(viewModel.Editor.SelectKey("KeyA"));
        var selectedKey = viewModel.Editor.SelectedKey;

        service.Apply(ApplicationTheme.White);
        service.Apply(ApplicationTheme.Black);

        Assert.Same(project, viewModel.Project);
        Assert.Same(editor, viewModel.Editor);
        Assert.Same(selectedKey, viewModel.Editor.SelectedKey);
        Assert.False(viewModel.IsDirty);
        Assert.Null(viewModel.CurrentFilePath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Constructor_WhenApplicationIsMissing_Rejects() =>
        Assert.Throws<ArgumentNullException>(() => new AvaloniaApplicationThemeService(null!));
}
