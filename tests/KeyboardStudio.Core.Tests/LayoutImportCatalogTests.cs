using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class LayoutImportCatalogTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ListAsync_WithSeveralSources_ConcatenatesThemInRegistrationOrder()
    {
        var catalog = new LayoutImportCatalog([
            new StubSource("first", ["a"]),
            new StubSource("second", ["b", "c"])
        ]);

        var descriptors = await catalog.ListAsync();

        Assert.Equal(
            ["first/a", "second/b", "second/c"],
            descriptors.Select(descriptor => $"{descriptor.SourceId}/{descriptor.LayoutId}"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ListAsync_WhenASourceIsUnavailable_PassesOverItWithoutQueryingIt()
    {
        // A host with no layout database is ordinary, not broken: the source says so up front and
        // the catalog must not make it prove it by throwing.
        var unavailable = new StubSource("absent", ["x"]) { IsAvailable = false };
        var catalog = new LayoutImportCatalog([unavailable, new StubSource("present", ["y"])]);

        var descriptors = await catalog.ListAsync();

        Assert.Equal(["present"], descriptors.Select(descriptor => descriptor.SourceId));
        Assert.Equal(0, unavailable.ListCallCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task ListAsync_WhenAnAvailableSourceFails_PropagatesRatherThanShorteningTheList()
    {
        // Silently returning fewer entries would leave the user hunting for a layout that is
        // installed, with nothing on screen to explain its absence.
        var catalog = new LayoutImportCatalog([new ThrowingSource()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.ListAsync());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_ForAReferenceFromTheCatalog_DispatchesToTheOwningSource()
    {
        var target = new StubSource("second", ["b"]);
        var catalog = new LayoutImportCatalog([new StubSource("first", ["a"]), target]);
        var descriptors = await catalog.ListAsync();
        var reference = descriptors.Single(descriptor => descriptor.SourceId == "second").ToReference();

        var result = await catalog.ImportAsync(reference, LayoutImportOptions.Default);

        Assert.True(result.Success);
        Assert.Equal(reference, target.LastImportedReference);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task ImportAsync_WhenNoSourceOwnsTheReference_ReportsAWiringFault()
    {
        var catalog = new LayoutImportCatalog([new StubSource("first", ["a"])]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => catalog.ImportAsync(
                new ImportableLayoutReference("nonexistent", "a"),
                LayoutImportOptions.Default));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task ImportAsync_WhenTheOwningSourceIsUnavailable_ReportsAWiringFault()
    {
        var catalog = new LayoutImportCatalog([new StubSource("first", ["a"]) { IsAvailable = false }]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => catalog.ImportAsync(
                new ImportableLayoutReference("first", "a"),
                LayoutImportOptions.Default));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HasAvailableSources_WhenEverySourceIsUnavailable_ReportsFalse()
    {
        var catalog = new LayoutImportCatalog([
            new StubSource("first", ["a"]) { IsAvailable = false },
            new StubSource("second", ["b"]) { IsAvailable = false }
        ]);

        Assert.False(catalog.HasAvailableSources);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HasAvailableSources_WhenOneSourceIsUsable_ReportsTrue()
    {
        var catalog = new LayoutImportCatalog([
            new StubSource("first", ["a"]) { IsAvailable = false },
            new StubSource("second", ["b"])
        ]);

        Assert.True(catalog.HasAvailableSources);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Constructor_WhenTwoSourcesShareAnId_RejectsTheRegistration()
    {
        // Source IDs are saved into documents as provenance, so a collision would make an imported
        // document's origin permanently ambiguous.
        var exception = Assert.Throws<ArgumentException>(
            () => new LayoutImportCatalog([
                new StubSource("duplicate", ["a"]),
                new StubSource("duplicate", ["b"])
            ]));

        Assert.Equal("sources", exception.ParamName);
    }

    private sealed class StubSource(string id, IReadOnlyList<string> layoutIds) : ILayoutImportSource
    {
        public string Id { get; } = id;

        public string DisplayName => Id;

        public bool IsAvailable { get; init; } = true;

        public int ListCallCount { get; private set; }

        public ImportableLayoutReference? LastImportedReference { get; private set; }

        public Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            ListCallCount++;
            IReadOnlyList<ImportableLayoutDescriptor> descriptors = layoutIds
                .Select(layoutId => new ImportableLayoutDescriptor(
                    Id,
                    layoutId,
                    null,
                    layoutId,
                    null,
                    [],
                    [],
                    LayoutSourceOrigin.System,
                    $"/stub/{Id}/{layoutId}"))
                .ToArray();
            return Task.FromResult(descriptors);
        }

        public Task<LayoutImportResult> ImportAsync(
            ImportableLayoutReference reference,
            LayoutImportOptions options,
            CancellationToken cancellationToken = default)
        {
            LastImportedReference = reference;
            var project = new KeyboardProject
            {
                Metadata = new ProjectMetadata { Name = reference.LayoutId },
                Keyboard = new PhysicalKeyboard { Id = "iso-105" },
                Layout = new KeyboardLayout()
            };
            return Task.FromResult(LayoutImportResult.Succeeded(
                project,
                "iso-105",
                new LayoutImportReport(LayoutImportFidelity.Exact, 0, 0, [], [])));
        }
    }

    private sealed class ThrowingSource : ILayoutImportSource
    {
        public string Id => "throwing";

        public string DisplayName => "Throwing";

        public bool IsAvailable => true;

        public Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The layout database could not be read.");

        public Task<LayoutImportResult> ImportAsync(
            ImportableLayoutReference reference,
            LayoutImportOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The layout database could not be read.");
    }
}
