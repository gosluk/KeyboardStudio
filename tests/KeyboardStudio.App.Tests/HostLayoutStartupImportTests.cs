using KeyboardStudio.App;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// What the editor does with the layout the host is already configured to type with: it opens onto
/// it when it can, and keeps whatever it started with when it cannot, without ever asking.
/// </summary>
public sealed class HostLayoutStartupImportTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhenTheHostLayoutImports_ReplacesTheStartingDocument()
    {
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog(),
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl", "qwertz"));

        await viewModel.ImportHostLayoutAsync();

        Assert.Equal("pl", viewModel.Project.Metadata.Name);
        Assert.Equal("ä", Assert.IsType<CharacterOutput>(
            viewModel.Project.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default]).Value);

        // Nothing has been written and nothing changed since it was made, exactly like the
        // document it replaced. An import nobody asked for must not leave unsaved work behind.
        Assert.False(viewModel.IsDirty);
        Assert.Null(viewModel.CurrentFilePath);
        Assert.Contains("pl(qwertz)", viewModel.ImportStatus, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhenTheHostLayoutImports_PrefillsTheXkbProfileFromIt()
    {
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog(),
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl", "qwertz"));

        await viewModel.ImportHostLayoutAsync();

        var linux = viewModel.Build.ExportTargetProfiles()[BuildProfileTargetIds.LinuxXkb];
        Assert.Equal("pl-custom", linux.Settings[BuildProfileKeys.LayoutId]);
        Assert.Equal("qwertz", linux.Settings[BuildProfileKeys.SectionId]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_LeavesNoGeometryChoiceToTheSourcesInference()
    {
        var catalog = new FakeLayoutImportCatalog();
        var viewModel = TestMainWindow.WithImportCatalog(
            catalog,
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl"));

        await viewModel.ImportHostLayoutAsync();

        // Nobody is at the dialog to correct a bad guess, so the source's own inference is the
        // best answer available and is taken unmodified.
        Assert.Null(catalog.LastOptions!.TemplateId);
        Assert.Equal(FakeLayoutImportCatalog.SuggestedTemplateId, viewModel.Project.Keyboard.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhenTheImportFails_KeepsTheSeedAndSaysSoInDiagnostics()
    {
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog { FailImport = true },
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl", "qwertz"));
        var seed = viewModel.Project;

        await viewModel.ImportHostLayoutAsync();

        Assert.Same(seed, viewModel.Project);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(StartupLayoutState.SeedFallback, viewModel.StartupState);
        Assert.Contains("built-in layout", viewModel.ImportStatus, StringComparison.Ordinal);

        var note = Assert.Single(
            viewModel.Diagnostics.Items,
            item => item.Code == LayoutImportDiagnosticCodes.HostLayoutUnavailable);
        Assert.Equal(ValidationSeverity.Info, note.Severity);
        Assert.Contains("pl(qwertz)", note.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_AfterAFailure_KeepsTheNoteThroughLaterEdits()
    {
        // Diagnostics are rebuilt from validation on every edit, so a note merely appended to the
        // list would disappear at the next keystroke.
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog { FailImport = true },
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl"));

        await viewModel.ImportHostLayoutAsync();
        viewModel.Editor.SelectKey("KeyA");
        viewModel.Editor.LayerMappings[0].Output = "q";

        Assert.Contains(
            viewModel.Diagnostics.Items,
            item => item.Code == LayoutImportDiagnosticCodes.HostLayoutUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_AfterAFailure_DropsTheNoteWhenAnotherDocumentTakesOver()
    {
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog { FailImport = true },
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl"));

        await viewModel.ImportHostLayoutAsync();
        await viewModel.NewCommand.ExecuteAsync(null);

        // The note explains a document that is no longer on screen.
        Assert.DoesNotContain(
            viewModel.Diagnostics.Items,
            item => item.Code == LayoutImportDiagnosticCodes.HostLayoutUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhenTheUserHasAlreadyEdited_LeavesTheirWorkAlone()
    {
        var catalog = new SlowLayoutImportCatalog();
        var viewModel = TestMainWindow.WithImportCatalog(
            catalog,
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl"));

        var startup = viewModel.ImportHostLayoutAsync();
        viewModel.Editor.SelectKey("KeyA");
        viewModel.Editor.LayerMappings[0].Output = "q";
        var edited = viewModel.Project;
        catalog.Release();
        await startup;

        Assert.Same(edited, viewModel.Project);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(string.Empty, viewModel.ImportStatus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhenTheProbeSaysNothing_DoesNothing()
    {
        var catalog = new FakeLayoutImportCatalog();
        var viewModel = TestMainWindow.WithImportCatalog(
            catalog,
            new SilentProjectInteractionService(),
            new FakeHostLayoutProbe(null));
        var seed = viewModel.Project;

        await viewModel.ImportHostLayoutAsync();

        Assert.Same(seed, viewModel.Project);
        Assert.Equal(0, catalog.ImportCount);
        Assert.Empty(viewModel.Diagnostics.Items);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WithNoSourceOnThisHost_NeverAsksTheProbe()
    {
        // A host with no keyboard database is an ordinary situation, not a failure, so there is
        // nothing to detect and nothing to report about not having detected it.
        var probe = FakeHostLayoutProbe.Detecting("pl");
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog { HasAvailableSources = false },
            new SilentProjectInteractionService(),
            probe);

        await viewModel.ImportHostLayoutAsync();

        Assert.Equal(0, probe.DetectCount);
        Assert.Empty(viewModel.Diagnostics.Items);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhileTheLayoutIsBeingRead_SaysSoWithoutBlockingTheEditor()
    {
        var catalog = new SlowLayoutImportCatalog();
        var viewModel = TestMainWindow.WithImportCatalog(
            catalog,
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl"));
        Assert.Equal(StartupLayoutState.NotStarted, viewModel.StartupState);

        var startup = viewModel.ImportHostLayoutAsync();

        Assert.Equal(StartupLayoutState.Loading, viewModel.StartupState);
        Assert.True(viewModel.IsLoadingCurrentLayout);
        Assert.Contains("current layout", viewModel.ImportStatus, StringComparison.OrdinalIgnoreCase);

        // The document on screen is editable throughout: nothing waits on the host.
        Assert.NotEmpty(viewModel.Project.Layout.Mappings);
        Assert.True(viewModel.Editor.SelectKey("KeyA"));

        catalog.Release();
        await startup;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhenTheHostLayoutImports_SettlesOnTheCurrentLayout()
    {
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog(),
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl", "qwertz"));

        await viewModel.ImportHostLayoutAsync();

        Assert.Equal(StartupLayoutState.CurrentLayout, viewModel.StartupState);
        Assert.False(viewModel.IsLoadingCurrentLayout);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhenNoLayoutCanBeRead_LeavesAPopulatedEditableSeed()
    {
        var viewModel = TestMainWindow.WithImportCatalog(
            new FakeLayoutImportCatalog { HasAvailableSources = false },
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl"));

        await viewModel.ImportHostLayoutAsync();

        Assert.Equal(StartupLayoutState.SeedFallback, viewModel.StartupState);
        Assert.NotEmpty(viewModel.Project.Layout.Mappings);
        Assert.Contains("built-in layout", viewModel.ImportStatus, StringComparison.Ordinal);

        // Editable straight away: the fallback is a document, not a placeholder.
        Assert.True(viewModel.Editor.SelectKey("KeyA"));
        viewModel.Editor.LayerMappings[0].Output = "z";
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportHostLayoutAsync_WhenTheUserActedFirst_RecordsThatTheResultWasDiscarded()
    {
        var catalog = new SlowLayoutImportCatalog();
        var viewModel = TestMainWindow.WithImportCatalog(
            catalog,
            new SilentProjectInteractionService(),
            FakeHostLayoutProbe.Detecting("pl"));

        var startup = viewModel.ImportHostLayoutAsync();
        viewModel.Editor.SelectKey("KeyA");
        viewModel.Editor.LayerMappings[0].Output = "q";
        catalog.Release();
        await startup;

        Assert.Equal(StartupLayoutState.Discarded, viewModel.StartupState);

        // The loading line belonged to a result that is not going to be used.
        Assert.Equal(string.Empty, viewModel.ImportStatus);
    }

    /// <summary>
    /// A catalog whose import blocks until the test lets it finish, so a test can do something in
    /// the window between the import starting and its result arriving.
    /// </summary>
    private sealed class SlowLayoutImportCatalog : ILayoutImportCatalog
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly FakeLayoutImportCatalog _inner = new();

        public bool HasAvailableSources => _inner.HasAvailableSources;

        public void Release() => _gate.TrySetResult();

        public Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
            CancellationToken cancellationToken = default) => _inner.ListAsync(cancellationToken);

        public async Task<LayoutImportResult> ImportAsync(
            ImportableLayoutReference reference,
            LayoutImportOptions options,
            CancellationToken cancellationToken = default)
        {
            await _gate.Task;
            return await _inner.ImportAsync(reference, options, cancellationToken);
        }
    }
}
