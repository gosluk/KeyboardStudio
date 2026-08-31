using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class SeedDocumentTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WhenCreated_OpensASeededDocumentRatherThanBareGeometry()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal("iso-105", viewModel.Project.Keyboard.Id);
        Assert.Equal(
            viewModel.Project.Keyboard.Keys.Count,
            viewModel.Project.Layout.Mappings.Count);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WhenCreated_ShowsTheSeedLegendsOnTheBoard()
    {
        var editor = new MainWindowViewModel().Editor;

        Assert.Equal("a", editor.Keys.Single(key => key.KeyId == "KeyA").DefaultAssignment);
        Assert.Equal("1", editor.Keys.Single(key => key.KeyId == "Digit1").DefaultAssignment);
        Assert.DoesNotContain(editor.Keys, key => key.IsUnmapped);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WhenCreated_ReportsNoValidationWarningsOrErrors()
    {
        var viewModel = new MainWindowViewModel();

        Assert.DoesNotContain(
            viewModel.Diagnostics.Items,
            item => item.Severity != ValidationSeverity.Info);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NewCommand_WhenAnotherTemplateIsChosen_KeepsTheSeedMappingsThatFit()
    {
        var viewModel = new MainWindowViewModel();

        await viewModel.NewDocumentOptions
            .Single(option => option.TemplateId == "ansi-104")
            .Command.ExecuteAsync(null);

        Assert.Equal("ansi-104", viewModel.Project.Keyboard.Id);
        Assert.NotEmpty(viewModel.Project.Layout.Mappings);

        // The seed is authored for ISO, so the two keys ANSI does not have are dropped and
        // ANSI's own Backslash key stays unmapped until the user fills it in.
        var mappedKeyIds = viewModel.Project.Layout.Mappings
            .Select(mapping => mapping.KeyId)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("IntlHash", mappedKeyIds);
        Assert.DoesNotContain("IntlBackslash", mappedKeyIds);
        Assert.DoesNotContain("Backslash", mappedKeyIds);
        Assert.Contains("KeyA", mappedKeyIds);

        Assert.All(
            viewModel.Project.Layout.Mappings,
            mapping => Assert.Contains(
                viewModel.Project.Keyboard.Keys,
                key => string.Equals(key.Id, mapping.KeyId, StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NewCommand_WhenInvokedTwice_DoesNotShareMappingStateBetweenDocuments()
    {
        var viewModel = new MainWindowViewModel(
            new SilentProjectInteractionService
            {
                ReplacementChoice = ProjectReplacementChoice.Discard
            });
        Assert.True(viewModel.Editor.SelectKey("KeyA"));
        viewModel.Editor.LayerMappings[0].Output = "z";

        await viewModel.NewCommand.ExecuteAsync(null);

        Assert.True(viewModel.Editor.SelectKey("KeyA"));
        Assert.Equal("a", viewModel.Editor.LayerMappings[0].Output);
    }
}
