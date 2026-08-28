using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class MainWindowDocumentLifecycleTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void MappingMutation_WhenProjectChanges_UpdatesDirtyPresentation()
    {
        var viewModel = new MainWindowViewModel();
        Assert.True(viewModel.Editor.SelectKey("KeyA"));

        // The seed already maps KeyA to "a"; the edit has to change something to mark the
        // document dirty.
        viewModel.Editor.LayerMappings[0].Output = "z";

        Assert.True(viewModel.IsDirty);
        Assert.Contains("*", viewModel.WindowTitle, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAsCommand_WhenPathIsSelected_PersistsAndClearsDirtyState()
    {
        var path = CreateTemporaryPath();
        try
        {
            var interaction = new TestProjectInteractionService { SavePath = path };
            var viewModel = new MainWindowViewModel(interaction);
            Assert.True(viewModel.Editor.SelectKey("KeyA"));
            viewModel.Editor.LayerMappings[0].Output = "z";

            await viewModel.SaveAsCommand.ExecuteAsync(null);

            Assert.False(viewModel.IsDirty);
            Assert.Equal(Path.GetFullPath(path), viewModel.CurrentFilePath);
            Assert.True(File.Exists(path));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NewCommand_WhenUnsavedReplacementIsCancelled_PreservesCurrentProject()
    {
        var interaction = new TestProjectInteractionService
        {
            ReplacementChoice = ProjectReplacementChoice.Cancel
        };
        var viewModel = new MainWindowViewModel(interaction);
        var originalProject = viewModel.Project;
        Assert.True(viewModel.Editor.SelectKey("KeyA"));
        viewModel.Editor.LayerMappings[0].Output = "z";
        viewModel.SelectedTemplate = viewModel.Templates.Single(template => template.Id == "ansi-104");

        await viewModel.NewCommand.ExecuteAsync(null);

        Assert.Same(originalProject, viewModel.Project);
        Assert.Equal("iso-105", viewModel.Project.Keyboard.Id);
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NewCommand_WhenUnsavedReplacementIsDiscarded_CreatesSelectedTemplateProject()
    {
        var interaction = new TestProjectInteractionService
        {
            ReplacementChoice = ProjectReplacementChoice.Discard
        };
        var viewModel = new MainWindowViewModel(interaction);
        Assert.True(viewModel.Editor.SelectKey("KeyA"));
        viewModel.Editor.LayerMappings[0].Output = "z";
        viewModel.SelectedTemplate = viewModel.Templates.Single(template => template.Id == "ansi-104");

        await viewModel.NewCommand.ExecuteAsync(null);

        Assert.Equal("ansi-104", viewModel.Project.Keyboard.Id);
        Assert.Equal(104, viewModel.Editor.Keys.Count);
        Assert.False(viewModel.IsDirty);
        Assert.Null(viewModel.CurrentFilePath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task OpenCommand_WhenProjectIsSelected_ReopensMappingsAndClearsDirtyState()
    {
        var path = CreateTemporaryPath();
        try
        {
            var sourceViewModel = new MainWindowViewModel();
            var source = sourceViewModel.Project;
            new KeyboardEditor(source).MapCharacter("KeyA", ModifierLayer.AltGr, "ą");
            await using (var stream = File.Create(path))
            {
                await new JsonKeyboardProjectDocumentStore().SaveAsync(
                    new KeyboardProjectDocument(
                        source,
                        sourceViewModel.Build.ExportTargetProfiles()),
                    stream);
            }

            var interaction = new TestProjectInteractionService
            {
                OpenPath = path,
                ReplacementChoice = ProjectReplacementChoice.Discard
            };
            var viewModel = new MainWindowViewModel(interaction);
            Assert.True(viewModel.Editor.SelectKey("KeyB"));
            viewModel.Editor.LayerMappings[0].Output = "b";

            await viewModel.OpenCommand.ExecuteAsync(null);

            var output = Assert.IsType<CharacterOutput>(
                viewModel.Project.Layout.Find("KeyA")?.Outputs[ModifierLayer.AltGr]);
            Assert.Equal("ą", output.Value);
            Assert.False(viewModel.IsDirty);
            Assert.Equal(Path.GetFullPath(path), viewModel.CurrentFilePath);
            Assert.Equal("iso-105", viewModel.SelectedTemplate.Id);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}.kbdproj");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class TestProjectInteractionService : IProjectInteractionService
    {
        public ProjectReplacementChoice ReplacementChoice { get; set; } =
            ProjectReplacementChoice.Cancel;

        public string? OpenPath { get; set; }

        public string? SavePath { get; set; }

        public List<string> Errors { get; } = [];

        public Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName) =>
            Task.FromResult(ReplacementChoice);

        public Task<string?> SelectOpenPathAsync() => Task.FromResult(OpenPath);

        public Task<string?> SelectSavePathAsync(string suggestedFileName) =>
            Task.FromResult(SavePath);

        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add($"{title}: {message}");
            return Task.CompletedTask;
        }
    }
}
