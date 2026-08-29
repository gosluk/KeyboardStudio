using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Importing a symbols file that lives outside every XKB root — one the user wrote, or one that
/// arrived with something other than the distribution.
/// </summary>
public sealed class XkbSymbolsFileImportSourceTests
{
    private const string SystemRoot = "/usr/share/X11/xkb";
    private const string LoosePath = "/home/user/layouts/mine";

    private const string LatinSymbols = """
        default partial alphanumeric_keys
        xkb_symbols "basic" {
            name[Group1] = "Latin";
            key <AD01> { [ q, Q ] };
            key <AD02> { [ w, W ] };
            key <AD03> { [ e, E ] };
        };
        """;

    private const string LooseSymbols = """
        default partial alphanumeric_keys
        xkb_symbols "basic" {
            include "latin(basic)"
            name[Group1] = "Mine";
            key <AD01> { [ z, Z ] };
        };

        partial alphanumeric_keys
        xkb_symbols "lefty" {
            include "mine(basic)"
            key <AD02> { [ y, Y ] };
        };
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_ForAFileOutsideEveryRoot_ResolvesItsIncludesFromTheInstalledDatabase()
    {
        var result = await CreateSource().ImportAsync(Reference(), LayoutImportOptions.Default);

        Assert.True(result.Success);

        // The file itself defines one key. The other two are what makes it a layout rather than a
        // patch, and they only exist in the installed database it composes from.
        Assert.Equal(3, result.Report.KeysImported);
        Assert.Equal(
            new CharacterOutput("z"),
            result.Project!.Layout.Find("KeyQ")!.Outputs[ModifierLayer.Default]);
        Assert.Equal(
            new CharacterOutput("w"),
            result.Project.Layout.Find("KeyW")!.Outputs[ModifierLayer.Default]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_WhenTheFileIncludesItsOwnSection_ReadsItFromTheFileRatherThanARoot()
    {
        // A root also holds a file called `mine`, with different content. The picked file wins,
        // the same way a user's own XKB root shadows the distribution's.
        var fileSystem = FileSystem().AddFile($"{SystemRoot}/symbols/mine", LatinSymbols);

        var result = await CreateSource(fileSystem).ImportAsync(
            Reference(variantId: "lefty"),
            LayoutImportOptions.Default);

        Assert.True(result.Success);
        Assert.Equal(
            new CharacterOutput("z"),
            result.Project!.Layout.Find("KeyQ")!.Outputs[ModifierLayer.Default]);
        Assert.Equal(
            new CharacterOutput("y"),
            result.Project.Layout.Find("KeyW")!.Outputs[ModifierLayer.Default]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_ForAFileTheRegistryCannotDescribe_SaysSo()
    {
        var result = await CreateSource().ImportAsync(Reference(), LayoutImportOptions.Default);

        Assert.Contains(
            result.Report.Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.LayoutMetadataUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_WhenNoFileIsAtThePath_FailsWithoutThrowing()
    {
        var result = await CreateSource().ImportAsync(
            new ImportableLayoutReference("linux-xkb-file", "gone", null, "/home/user/layouts/gone"),
            LayoutImportOptions.Default);

        Assert.False(result.Success);
        Assert.Contains(
            result.Report.Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.CompositionTargetUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_WhenTheFileHasNoSuchSection_FailsWithoutThrowing()
    {
        var result = await CreateSource().ImportAsync(
            Reference(variantId: "nosuch"),
            LayoutImportOptions.Default);

        Assert.False(result.Success);
        Assert.Contains("nosuch", result.Report.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ListAsync_AlwaysReturnsNothing()
    {
        // A file outside the roots is not something to browse for. Listing it would mean guessing
        // where the user keeps their layouts, which is not a guess worth making.
        Assert.Empty(await CreateSource().ListAsync());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsAvailable_WithNoInstalledDatabase_IsFalse()
    {
        // Symbols files are written as differences from the database. Without one, importing a
        // file yields the handful of keys it overrides and a report full of missing includes.
        var source = new XkbSymbolsFileImportSource(
            new FakeXkbFileSystem(),
            new StubDataRootLocator([]),
            new XkbKeyNameMapper(),
            new XkbKeysymDecoder(),
            new KeyboardTemplateProvider());

        Assert.False(source.IsAvailable);
    }

    private static ImportableLayoutReference Reference(string? variantId = null) =>
        new("linux-xkb-file", "mine", variantId, LoosePath);

    private static FakeXkbFileSystem FileSystem() =>
        new FakeXkbFileSystem()
            .AddFile($"{SystemRoot}/symbols/latin", LatinSymbols)
            .AddFile(LoosePath, LooseSymbols);

    private static XkbSymbolsFileImportSource CreateSource(FakeXkbFileSystem? fileSystem = null)
    {
        fileSystem ??= FileSystem();

        return new XkbSymbolsFileImportSource(
            fileSystem,
            new StubDataRootLocator([new XkbDataRoot(SystemRoot, LayoutSourceOrigin.System)]),
            new XkbKeyNameMapper(),
            new XkbKeysymDecoder(),
            new KeyboardTemplateProvider());
    }

    private sealed class StubDataRootLocator(IReadOnlyList<XkbDataRoot> roots) : IXkbDataRootLocator
    {
        public IReadOnlyList<XkbDataRoot> Locate() => roots;
    }
}
