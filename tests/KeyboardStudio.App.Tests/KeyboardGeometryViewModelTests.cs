using KeyboardStudio.App;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class KeyboardGeometryViewModelTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WhenCreated_UsesIsoTemplateGeometryByDefault()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal("iso-105", viewModel.SelectedTemplate.Id);
        Assert.Equal("iso-105", viewModel.Project.Keyboard.Id);
        Assert.Equal(105, viewModel.Editor.Keys.Count);
        Assert.Equal(1330, viewModel.Editor.KeyboardWidth);
        Assert.Equal(373, viewModel.Editor.KeyboardHeight);

        var enter = Assert.Single(viewModel.Editor.Keys, key => key.KeyId == "Enter");
        Assert.Equal(797.5, enter.Left);
        Assert.Equal(145, enter.Top);
        Assert.Equal(68.5, enter.Width);
        Assert.Equal(112, enter.Height);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NewCommand_WhenAnsiTemplateIsChosen_RebuildsEditorWithAnsiGeometry()
    {
        var viewModel = new MainWindowViewModel();
        var ansiTemplate = Assert.Single(viewModel.Templates, template => template.Id == "ansi-104");

        viewModel.SelectedTemplate = ansiTemplate;
        await viewModel.NewCommand.ExecuteAsync(null);

        Assert.Equal("ansi-104", viewModel.Project.Keyboard.Id);
        Assert.Equal(104, viewModel.Editor.Keys.Count);
        Assert.Equal(1330, viewModel.Editor.KeyboardWidth);
        Assert.Equal(373, viewModel.Editor.KeyboardHeight);

        var backslash = Assert.Single(viewModel.Editor.Keys, key => key.KeyId == "Backslash");
        Assert.Equal(783, backslash.Left);
        Assert.Equal(145, backslash.Top);
        Assert.Equal(83, backslash.Width);
        Assert.Equal(54, backslash.Height);

        var enter = Assert.Single(viewModel.Editor.Keys, key => key.KeyId == "Enter");
        Assert.Equal(739.5, enter.Left);
        Assert.Equal(203, enter.Top);
        Assert.Equal(126.5, enter.Width);
        Assert.Equal(54, enter.Height);
    }
}
