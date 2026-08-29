using KeyboardStudio.App;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// The import dialog's behaviour: how a flat catalog of several hundred entries becomes something
/// choosable, and what selecting an entry costs before anything is committed.
/// </summary>
public sealed class LayoutImportViewModelTests
{
    private static readonly IReadOnlyList<KeyboardTemplateDescriptor> Templates =
        new KeyboardTemplateProvider().Templates;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_GroupsVariantsUnderTheirLayoutWithTheDefaultFirst()
    {
        var viewModel = Create(Catalog());

        await viewModel.LoadAsync();

        var polish = Assert.Single(viewModel.Layouts, layout => layout.LayoutId == "pl");
        Assert.Equal("Polish", polish.DisplayName);
        Assert.Equal(["Default", "Polish (QWERTZ)"], polish.Variants.Select(variant => variant.DisplayName));
        Assert.Null(polish.Variants[0].VariantId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_SelectsAndImportsTheFirstEntry()
    {
        // An import dialog that opens onto an empty pane makes the user click before it will tell
        // them anything, so it opens showing the first entry already read.
        var viewModel = Create(Catalog());

        await viewModel.LoadAsync();
        await viewModel.PreviewTask;

        Assert.Equal("de", viewModel.SelectedLayout?.LayoutId);
        Assert.NotNull(viewModel.Report);
        Assert.True(viewModel.CanAccept);
        Assert.NotEmpty(viewModel.PreviewKeys);

        // The import's own account of itself supersedes the catalog's size, which is the older
        // and less useful of the two things that could be on the status line.
        Assert.StartsWith("German:", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenNothingIsAvailable_SaysSoRatherThanShowingAnEmptyList()
    {
        var viewModel = Create(new FakeLayoutImportCatalog());

        await viewModel.LoadAsync();

        Assert.False(viewModel.HasLayouts);
        Assert.False(viewModel.CanAccept);
        Assert.Contains("No layouts", viewModel.Status, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("polish")]
    [InlineData("pl")]
    [InlineData("pol")]
    [InlineData("PL")]
    public async Task SearchText_ReachesALayoutByName_CodeOrLanguage(string search)
    {
        // A user looking for their own keyboard rarely knows which of the three the distribution
        // wrote down.
        var viewModel = Create(Catalog());
        await viewModel.LoadAsync();

        viewModel.SearchText = search;

        Assert.Equal("pl", Assert.Single(viewModel.Layouts).LayoutId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchText_WhenNothingMatches_SaysSoRatherThanLeavingTheLastImportOnScreen()
    {
        var viewModel = Create(Catalog());
        await viewModel.LoadAsync();
        await viewModel.PreviewTask;

        viewModel.SearchText = "klingon";

        Assert.False(viewModel.HasLayouts);
        Assert.Contains("klingon", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchText_WhenTheSelectionStillMatches_KeepsIt()
    {
        var catalog = Catalog();
        var viewModel = Create(catalog);
        await viewModel.LoadAsync();
        viewModel.SearchText = "Polish";
        viewModel.SelectedVariant = viewModel.SelectedLayout!.Variants[1];
        await viewModel.PreviewTask;
        var importsBefore = catalog.ImportCount;

        viewModel.SearchText = "Polish (";

        // Typing narrows the list without throwing away a layout the user has already chosen and
        // waited for, so nothing is imported a second time either.
        Assert.Equal("pl", viewModel.SelectedLayout?.LayoutId);
        Assert.Equal("qwertz", viewModel.SelectedVariant?.VariantId);
        Assert.Equal(importsBefore, catalog.ImportCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectedVariant_WhenChanged_ImportsThatVariant()
    {
        var catalog = Catalog();
        var viewModel = Create(catalog);
        await viewModel.LoadAsync();
        viewModel.SelectedLayout = viewModel.Layouts.Single(layout => layout.LayoutId == "pl");

        viewModel.SelectedVariant = viewModel.Variants[1];
        await viewModel.PreviewTask;

        Assert.Equal("pl", catalog.LastReference?.LayoutId);
        Assert.Equal("qwertz", catalog.LastReference?.VariantId);
        Assert.Equal("Polish (QWERTZ)", catalog.LastOptions?.ProjectName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UseSuggestedGeometry_WhenSet_LeavesTheChoiceToTheSourceAndShowsWhatItChose()
    {
        var catalog = Catalog();
        var viewModel = Create(catalog);

        await viewModel.LoadAsync();
        await viewModel.PreviewTask;

        Assert.Null(catalog.LastOptions?.TemplateId);
        Assert.Equal(FakeLayoutImportCatalog.SuggestedTemplateId, viewModel.SelectedTemplate.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UseSuggestedGeometry_WhenCleared_ImportsOntoTheChosenGeometry()
    {
        // The registry does not record physical geometry, so the inference is a guess and the user
        // has to be able to overrule it.
        var catalog = Catalog();
        var viewModel = Create(catalog);
        await viewModel.LoadAsync();
        await viewModel.PreviewTask;

        viewModel.UseSuggestedGeometry = false;
        viewModel.SelectedTemplate = Templates.Single(template => template.Id == "iso-105");
        await viewModel.PreviewTask;

        Assert.Equal("iso-105", catalog.LastOptions?.TemplateId);
        Assert.Equal("iso-105", viewModel.Result?.Project?.Keyboard.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CommitMode_WhenReplacingMappings_PinsTheGeometryToTheOpenDocument()
    {
        // Replacing mappings keeps the open document's keyboard, so a second geometry would only
        // invite keys that cannot fit on it.
        var catalog = Catalog();
        var current = Templates.Single(template => template.Id == "iso-105");
        var viewModel = Create(catalog, current);
        await viewModel.LoadAsync();
        await viewModel.PreviewTask;

        viewModel.CommitMode = LayoutImportCommitMode.ReplaceMappings;
        await viewModel.PreviewTask;

        Assert.False(viewModel.IsGeometrySelectable);
        Assert.Equal("iso-105", catalog.LastOptions?.TemplateId);
        Assert.True(viewModel.CommitAsMappingReplacement);
        Assert.False(viewModel.CommitAsNewProject);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CommitMode_WithNoOpenDocumentGeometry_CannotReplaceMappings()
    {
        var viewModel = Create(Catalog());
        await viewModel.LoadAsync();

        viewModel.CommitMode = LayoutImportCommitMode.ReplaceMappings;

        Assert.False(viewModel.CanReplaceMappings);
        Assert.Equal(LayoutImportCommitMode.NewProject, viewModel.CommitMode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PreviewAsync_WhenTheImportFails_ReportsItAndRefusesToCommit()
    {
        var viewModel = Create(new FakeLayoutImportCatalog { FailImport = true }.Add("pl", null, "Polish"));

        await viewModel.LoadAsync();
        await viewModel.PreviewTask;

        Assert.False(viewModel.CanAccept);
        Assert.Equal(LayoutImportFidelity.Partial, viewModel.Report?.Fidelity);
        Assert.Empty(viewModel.PreviewKeys);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForDescriptor_PinsOneEntryAndHidesTheSearch()
    {
        var descriptor = new ImportableLayoutDescriptor(
            "fake",
            "mine",
            null,
            "mine",
            "/home/user/layouts/mine",
            [],
            [],
            LayoutSourceOrigin.File,
            "/home/user/layouts/mine");
        var catalog = Catalog();

        var viewModel = LayoutImportViewModel.ForDescriptor(catalog, Templates, descriptor);
        await viewModel.LoadAsync();
        await viewModel.PreviewTask;

        Assert.False(viewModel.IsSearchable);
        Assert.Equal("mine", Assert.Single(viewModel.Layouts).LayoutId);
        Assert.Equal("/home/user/layouts/mine", catalog.LastReference?.SourceLocation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithNoTemplates_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new LayoutImportViewModel(Catalog(), []));
    }

    private static FakeLayoutImportCatalog Catalog() =>
        new FakeLayoutImportCatalog()
            .Add("pl", null, "Polish", ["pol"], ["PL"])
            .Add("pl", "qwertz", "Polish (QWERTZ)", ["pol"], ["PL"])
            .Add("de", null, "German", ["deu"], ["DE"]);

    private static LayoutImportViewModel Create(
        ILayoutImportCatalog catalog,
        KeyboardTemplateDescriptor? currentTemplate = null) =>
        new(catalog, Templates, currentTemplate);
}
