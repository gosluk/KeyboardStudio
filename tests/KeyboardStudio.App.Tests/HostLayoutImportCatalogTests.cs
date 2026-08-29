using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// The import composition root. Every other import test runs against a fake catalog, which proves
/// the dialog and the commit paths but says nothing about whether the real sources are wired up —
/// the one thing only the real host can answer.
/// </summary>
public sealed class HostLayoutImportCatalogTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void DescribeFile_NamesTheFileAndPointsTheFileSourceAtIt()
    {
        // Built from the host rather than written as a literal. This path is the one the user
        // picked in a file dialog, so it belongs to whatever host is running, and resolving it is
        // the method's job: a POSIX literal on Windows resolves against the current drive, which
        // says nothing about the descriptor and everything about the test's input.
        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "layouts", "mine"));

        var descriptor = HostLayoutImportCatalog.DescribeFile(path);

        Assert.Equal(XkbSymbolsFileImportSource.SourceId, descriptor.SourceId);
        Assert.Equal("mine", descriptor.LayoutId);
        Assert.Null(descriptor.VariantId);
        Assert.Equal(LayoutSourceOrigin.File, descriptor.Origin);
        Assert.Equal(path, descriptor.SourceLocation);
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task Create_OnAHostWithAnXkbDatabase_ListsAndImportsARealLayout()
    {
        var catalog = HostLayoutImportCatalog.Create(new KeyboardTemplateProvider());
        if (!HasInstalledDatabase(catalog))
        {
            return;
        }

        var descriptors = await catalog.ListAsync();
        Assert.True(descriptors.Count > 500, $"Expected a full catalog; got {descriptors.Count} entries.");

        var us = descriptors.FirstOrDefault(descriptor =>
            descriptor.LayoutId == "us" && descriptor.VariantId is null);
        Assert.NotNull(us);

        var result = await catalog.ImportAsync(us.ToReference(), LayoutImportOptions.Default);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Project!.Keyboard.Keys);
        Assert.NotEmpty(result.Project.Layout.Mappings);
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task Create_ImportsThroughTheDialogsViewModelExactlyAsTheApplicationWould()
    {
        var templateProvider = new KeyboardTemplateProvider();
        var catalog = HostLayoutImportCatalog.Create(templateProvider);
        if (!HasInstalledDatabase(catalog))
        {
            return;
        }

        var viewModel = new LayoutImportViewModel(catalog, templateProvider.Templates);
        await viewModel.LoadAsync();
        await viewModel.PreviewTask;

        // The empty-keyboard defect P13.9 fixed was invisible until a project was rendered, so the
        // preview is asserted here rather than only the import's own report.
        Assert.True(viewModel.Layouts.Count > 50, $"Expected a full catalog; got {viewModel.Layouts.Count} layouts.");
        Assert.True(viewModel.CanAccept);
        Assert.NotEmpty(viewModel.PreviewKeys);
        Assert.True(viewModel.PreviewWidth > 0);
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task CreateHostProbe_NamesALayoutThisHostCanActuallyImport()
    {
        // The startup import is the one path that runs with nobody watching, so the only thing
        // that can tell whether the probe and the catalog agree on their vocabulary is a host
        // that has both a configuration and a database.
        var templateProvider = new KeyboardTemplateProvider();
        var catalog = HostLayoutImportCatalog.Create(templateProvider);
        if (!HasInstalledDatabase(catalog))
        {
            return;
        }

        var reference = HostLayoutImportCatalog.CreateHostProbe().Detect();
        Assert.NotNull(reference);

        var result = await catalog.ImportAsync(reference, LayoutImportOptions.Default);

        Assert.True(result.Success, $"Importing this host's own layout '{reference.LayoutId}' failed.");
        Assert.NotEmpty(result.Project!.Layout.Mappings);
    }

    /// <summary>
    /// Whether this host has an XKB database to test against. A developer machine without one
    /// skips; Linux CI installs the package deliberately, so an absence there is a broken workflow
    /// rather than a host to accommodate, and fails loudly instead of passing quietly.
    /// </summary>
    private static bool HasInstalledDatabase(ILayoutImportCatalog catalog)
    {
        if (catalog.HasAvailableSources)
        {
            return true;
        }

        if (OperatingSystem.IsLinux() &&
            string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail("An installed XKB database is required for XkbIntegration tests in Linux CI.");
        }

        return false;
    }
}
