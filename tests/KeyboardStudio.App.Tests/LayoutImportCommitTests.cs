using KeyboardStudio.App;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// What accepting an import does to the open document: which of the two commit paths ran, what
/// survived it, and what the saved file then says about where the layout came from.
/// </summary>
public sealed class LayoutImportCommitTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportLayoutCommand_WhenAccepted_ReplacesTheDocumentWithTheImportedLayout()
    {
        var interaction = new ImportingInteractionService();
        var viewModel = TestMainWindow.WithImportCatalog(Catalog(), interaction);

        await viewModel.ImportLayoutCommand.ExecuteAsync(null);

        Assert.Equal("Polish", viewModel.Project.Metadata.Name);
        Assert.Equal(FakeLayoutImportCatalog.SuggestedTemplateId, viewModel.Project.Keyboard.Id);
        Assert.Equal("ä", Assert.IsType<CharacterOutput>(
            viewModel.Project.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default]).Value);

        // A fresh import is a fresh document: no file behind it, and nothing changed since it was
        // made — the same state a new document is in.
        Assert.Null(viewModel.CurrentFilePath);
        Assert.False(viewModel.IsDirty);
        Assert.Contains("pl", viewModel.ImportStatus, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportLayoutCommand_WhenAccepted_PrefillsTheXkbProfileWithASuffixedLayoutId()
    {
        // An artifact named symbols/pl would shadow the distribution's own file if it were copied
        // into an XKB root, so the imported project never builds under the source's own ID.
        var interaction = new ImportingInteractionService { VariantIndex = 1 };
        var viewModel = TestMainWindow.WithImportCatalog(Catalog(), interaction);

        await viewModel.ImportLayoutCommand.ExecuteAsync(null);

        var linux = viewModel.Build.ExportTargetProfiles()[BuildProfileTargetIds.LinuxXkb];
        Assert.Equal("pl-custom", linux.Settings[BuildProfileKeys.LayoutId]);
        Assert.Equal("qwertz", linux.Settings[BuildProfileKeys.SectionId]);
        Assert.Equal("Polish (QWERTZ)", linux.Settings[BuildProfileKeys.Description]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportLayoutCommand_ForALayoutWithNoVariant_UsesTheDefaultSection()
    {
        var interaction = new ImportingInteractionService();
        var viewModel = TestMainWindow.WithImportCatalog(Catalog(), interaction);

        await viewModel.ImportLayoutCommand.ExecuteAsync(null);

        var linux = viewModel.Build.ExportTargetProfiles()[BuildProfileTargetIds.LinuxXkb];
        Assert.Equal("pl-custom", linux.Settings[BuildProfileKeys.LayoutId]);
        Assert.Equal("basic", linux.Settings[BuildProfileKeys.SectionId]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportLayoutCommand_WhenAccepted_RecordsProvenanceThatSurvivesASaveAndReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kbdproj-{Guid.NewGuid():N}.kbdproj");
        try
        {
            var interaction = new ImportingInteractionService { SavePath = path, VariantIndex = 1 };
            var viewModel = TestMainWindow.WithImportCatalog(Catalog(), interaction);
            await viewModel.ImportLayoutCommand.ExecuteAsync(null);

            await viewModel.SaveAsCommand.ExecuteAsync(null);

            await using var stream = File.OpenRead(path);
            var document = await new JsonKeyboardProjectDocumentStore().LoadAsync(stream);
            var provenance = Assert.IsType<LayoutImportProvenance>(document.ImportProvenance);
            Assert.Equal("fake", provenance.SourceId);
            Assert.Equal("pl", provenance.LayoutId);
            Assert.Equal("qwertz", provenance.VariantId);
            Assert.Equal("/xkb/symbols/pl", provenance.SourceLocation);
            Assert.Equal("Polish (QWERTZ)", provenance.SourceDescription);
            Assert.NotEqual(default, provenance.ImportedAtUtc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportLayoutCommand_WhenReplacingMappings_KeepsGeometryProfilesAndFilePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kbdproj-{Guid.NewGuid():N}.kbdproj");
        try
        {
            var interaction = new ImportingInteractionService
            {
                SavePath = path,
                CommitMode = LayoutImportCommitMode.ReplaceMappings
            };
            var viewModel = TestMainWindow.WithImportCatalog(Catalog(), interaction);
            await viewModel.SaveAsCommand.ExecuteAsync(null);
            var geometryBefore = viewModel.Project.Keyboard.Id;
            viewModel.Build.SelectedTarget = viewModel.Build.Targets.Single(
                target => target.Target == BuildTarget.LinuxXkb);
            viewModel.Build.ProfileSettings.Single(
                setting => setting.Key == BuildProfileKeys.LayoutId).Value = "authored";

            await viewModel.ImportLayoutCommand.ExecuteAsync(null);

            Assert.Equal(geometryBefore, viewModel.Project.Keyboard.Id);
            Assert.Equal(Path.GetFullPath(path), viewModel.CurrentFilePath);
            Assert.Equal(
                "authored",
                viewModel.Build.ExportTargetProfiles()[BuildProfileTargetIds.LinuxXkb]
                    .Settings[BuildProfileKeys.LayoutId]);

            // Replacement, not merge: the seed's mappings are gone and the import's are all there is.
            var mapping = Assert.Single(viewModel.Project.Layout.Mappings);
            Assert.Equal("KeyA", mapping.KeyId);
            Assert.True(viewModel.IsDirty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportLayoutCommand_WhenTheUnsavedPromptIsCancelled_KeepsTheCurrentProject()
    {
        var interaction = new ImportingInteractionService
        {
            ReplacementChoice = ProjectReplacementChoice.Cancel
        };
        var viewModel = TestMainWindow.WithImportCatalog(Catalog(), interaction);
        Assert.True(viewModel.Editor.SelectKey("KeyA"));
        viewModel.Editor.LayerMappings[0].Output = "z";
        var project = viewModel.Project;

        await viewModel.ImportLayoutCommand.ExecuteAsync(null);

        Assert.Same(project, viewModel.Project);
        Assert.Empty(viewModel.ImportStatus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NewCommand_AfterAnImport_DoesNotInheritTheImportStatus()
    {
        var viewModel = TestMainWindow.WithImportCatalog(Catalog(), new ImportingInteractionService());
        await viewModel.ImportLayoutCommand.ExecuteAsync(null);
        Assert.NotEmpty(viewModel.ImportStatus);

        await viewModel.NewCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.ImportStatus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportLayoutCommand_WhenTheDialogIsDismissed_ChangesNothing()
    {
        var catalog = Catalog();
        var interaction = new ImportingInteractionService { Accept = false };
        var viewModel = TestMainWindow.WithImportCatalog(catalog, interaction);
        var project = viewModel.Project;

        await viewModel.ImportLayoutCommand.ExecuteAsync(null);

        Assert.Same(project, viewModel.Project);
        Assert.Empty(viewModel.ImportStatus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportFromFileCommand_WhenAFileIsPicked_ImportsItThroughTheCatalog()
    {
        var catalog = Catalog();
        var interaction = new ImportingInteractionService
        {
            SymbolsFilePath = Path.Combine(Path.GetTempPath(), "mine")
        };
        var viewModel = TestMainWindow.WithImportCatalog(catalog, interaction);

        await viewModel.ImportFromFileCommand.ExecuteAsync(null);

        // The file is named, not browsed for, so the reference carries a path rather than a
        // catalogue identifier the source could look up.
        Assert.Equal("mine", catalog.LastReference?.LayoutId);
        Assert.Equal(interaction.SymbolsFilePath, catalog.LastReference?.SourceLocation);
        Assert.Equal("mine", viewModel.Project.Metadata.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportFromFileCommand_WhenNoFileIsPicked_DoesNothing()
    {
        var catalog = Catalog();
        var viewModel = TestMainWindow.WithImportCatalog(catalog, new ImportingInteractionService());
        var project = viewModel.Project;

        await viewModel.ImportFromFileCommand.ExecuteAsync(null);

        Assert.Same(project, viewModel.Project);
        Assert.Equal(0, catalog.ImportCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ImportCommands_WhenNoSourceIsAvailable_AreDisabled()
    {
        // A host with no keyboard database gets the action disabled rather than a dialog that
        // opens onto nothing.
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog { HasAvailableSources = false },
            new ImportingInteractionService());

        Assert.False(viewModel.CanImportLayout);
        Assert.False(viewModel.ImportLayoutCommand.CanExecute(null));
        Assert.False(viewModel.ImportFromFileCommand.CanExecute(null));
    }

    private static FakeLayoutImportCatalog Catalog() =>
        new FakeLayoutImportCatalog()
            .Add("pl", null, "Polish", ["pol"], ["PL"])
            .Add("pl", "qwertz", "Polish (QWERTZ)", ["pol"], ["PL"]);

    /// <summary>
    /// An interaction service that accepts the import dialog, having first made the choices a user
    /// would have made in it.
    /// </summary>
    private sealed class ImportingInteractionService : IProjectInteractionService
    {
        public bool Accept { get; init; } = true;

        public int VariantIndex { get; init; }

        public LayoutImportCommitMode CommitMode { get; init; } = LayoutImportCommitMode.NewProject;

        public ProjectReplacementChoice ReplacementChoice { get; init; } =
            ProjectReplacementChoice.Discard;

        public string? SavePath { get; init; }

        public string? SymbolsFilePath { get; init; }

        public Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName) =>
            Task.FromResult(ReplacementChoice);

        public Task<string?> SelectOpenPathAsync() => Task.FromResult<string?>(null);

        public Task<string?> SelectSavePathAsync(string suggestedFileName) =>
            Task.FromResult(SavePath);

        public async Task<bool> ShowLayoutImportAsync(LayoutImportViewModel viewModel)
        {
            if (VariantIndex > 0)
            {
                viewModel.SelectedVariant = viewModel.Variants[VariantIndex];
            }

            viewModel.CommitMode = CommitMode;
            await viewModel.PreviewTask;
            return Accept;
        }

        public Task<string?> SelectSymbolsFilePathAsync() => Task.FromResult(SymbolsFilePath);

        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
    }
}
