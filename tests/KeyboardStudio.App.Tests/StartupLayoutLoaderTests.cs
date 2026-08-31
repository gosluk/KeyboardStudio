using KeyboardStudio.Core;
using KeyboardStudio.Testing;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// The loader reads; it never decides. Every outcome it can reach comes back as a result, because
/// an import nobody asked for must not be able to throw into a window that is already drawing a
/// working document.
/// </summary>
public sealed class StartupLayoutLoaderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenTheHostLayoutImports_ReturnsTheProjectAndItsReference()
    {
        var loader = new StartupLayoutLoader(
            new FakeLayoutImportCatalog(),
            FakeHostLayoutProbe.Detecting("pl", "qwertz"));

        var result = await loader.LoadAsync();

        Assert.Equal(StartupLayoutStatus.Imported, result.Status);
        Assert.Equal("pl", result.Reference?.LayoutId);
        Assert.Equal("qwertz", result.Reference?.VariantId);
        Assert.Equal("pl", result.Project?.Metadata.Name);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_LeavesTheGeometryToTheSourcesOwnInference()
    {
        var catalog = new FakeLayoutImportCatalog();
        var loader = new StartupLayoutLoader(catalog, FakeHostLayoutProbe.Detecting("pl"));

        await loader.LoadAsync();

        // Nobody is at the dialog to correct a bad guess, so the source's inference is the best
        // answer available and is taken unmodified.
        Assert.Null(catalog.LastOptions!.TemplateId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenNoSourceCanListALayout_NeverAsksTheProbe()
    {
        var probe = FakeHostLayoutProbe.Detecting("pl");
        var loader = new StartupLayoutLoader(
            new FakeLayoutImportCatalog { HasAvailableSources = false },
            probe);

        var result = await loader.LoadAsync();

        Assert.Equal(StartupLayoutStatus.Unavailable, result.Status);
        Assert.Equal(0, probe.DetectCount);
        Assert.Null(result.Reference);
        Assert.Null(result.Project);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenTheProbeSaysNothing_ImportsNothing()
    {
        var catalog = new FakeLayoutImportCatalog();
        var loader = new StartupLayoutLoader(catalog, new FakeHostLayoutProbe(null));

        var result = await loader.LoadAsync();

        Assert.Equal(StartupLayoutStatus.Unavailable, result.Status);
        Assert.Equal(0, catalog.ImportCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task LoadAsync_WhenTheImportFails_ReportsTheReferenceItFailedFor()
    {
        var loader = new StartupLayoutLoader(
            new FakeLayoutImportCatalog { FailImport = true },
            FakeHostLayoutProbe.Detecting("pl", "qwertz"));

        var result = await loader.LoadAsync();

        Assert.Equal(StartupLayoutStatus.Failed, result.Status);
        Assert.Equal("pl", result.Reference?.LayoutId);
        Assert.Null(result.Project);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
    }

    [Theory]
    [MemberData(nameof(HostFailures))]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task LoadAsync_WhenTheHostThrows_ReportsItRatherThanRaisingIt(Exception exception)
    {
        var loader = new StartupLayoutLoader(
            new ThrowingLayoutImportCatalog(() => exception),
            FakeHostLayoutProbe.Detecting("pl"));

        var result = await loader.LoadAsync();

        Assert.Equal(StartupLayoutStatus.Failed, result.Status);
        Assert.Equal(exception.Message, result.FailureReason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenCancelled_ReportsCancellationRatherThanRaisingIt()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var loader = new StartupLayoutLoader(
            new ThrowingLayoutImportCatalog(() => new OperationCanceledException()),
            FakeHostLayoutProbe.Detecting("pl"));

        var result = await loader.LoadAsync(cancellation.Token);

        Assert.Equal(StartupLayoutStatus.Cancelled, result.Status);
        Assert.Null(result.Project);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Constructor_WhenACollaboratorIsMissing_Rejects()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StartupLayoutLoader(null!, new FakeHostLayoutProbe(null)));
        Assert.Throws<ArgumentNullException>(
            () => new StartupLayoutLoader(new FakeLayoutImportCatalog(), null!));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Result_CannotClaimAnOutcomeItHasNoDataFor()
    {
        var reference = new ImportableLayoutReference("fake", "pl", null);

        Assert.Throws<ArgumentNullException>(() => StartupLayoutResult.Imported(reference, null!));
        Assert.Throws<ArgumentNullException>(
            () => StartupLayoutResult.Imported(null!, TestProjectFactory.Create()));
        Assert.Throws<ArgumentException>(() => StartupLayoutResult.Failed(reference, "  "));
    }

    public static TheoryData<Exception> HostFailures() =>
    [
        new IOException("the symbols file could not be read"),
        new UnauthorizedAccessException("the keyboard database is not readable"),
        new InvalidOperationException("the rules registry is malformed"),
    ];
}
