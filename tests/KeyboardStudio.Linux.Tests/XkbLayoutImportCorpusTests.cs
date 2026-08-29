using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;
using Xunit.Abstractions;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Imports the host's whole installed catalog. Hand-written sections model the shape of a symbols
/// file well and its variety not at all: only the real database says whether every entry the
/// catalog offers can actually be imported. Skipped where no XKB database is installed, and
/// enforced in Linux CI.
/// </summary>
public sealed class XkbLayoutImportCorpusTests(ITestOutputHelper output)
{
    private const string RootDirectory = "/usr/share/X11/xkb";

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task ImportAsync_ForEveryEntryTheCatalogOffers_ProducesAUsableProject()
    {
        if (!TryCreateSource(out var source))
        {
            return;
        }

        var descriptors = await source.ListAsync();
        var validator = new KeyboardProjectValidator();
        var failed = new List<string>();
        var unmappable = new List<string>();
        var invalid = new List<string>();
        var keysImported = 0;

        foreach (var descriptor in descriptors)
        {
            var result = await source.ImportAsync(descriptor.ToReference(), LayoutImportOptions.Default);
            var name = Describe(descriptor);

            if (!result.Success)
            {
                failed.Add(name);
                continue;
            }

            keysImported += result.Report.KeysImported;

            // A project whose mappings address keys its keyboard does not have renders as an empty
            // board, so the geometry has to travel with the layout that was laid onto it.
            var keyIds = result.Project!.Keyboard.Keys.Select(key => key.Id).ToHashSet(StringComparer.Ordinal);
            unmappable.AddRange(result.Project.Layout.Mappings
                .Where(mapping => !keyIds.Contains(mapping.KeyId))
                .Select(mapping => $"{name}: {mapping.KeyId}"));

            // Whatever the catalog offers has to arrive as a document the editor will accept.
            // An import that succeeds and then cannot be validated or built is a dead end the
            // user reaches only after choosing it.
            invalid.AddRange(validator.Validate(result.Project)
                .Issues
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .Select(issue => $"{name}: {issue.Message}"));
        }

        output.WriteLine($"{descriptors.Count} entries, {keysImported} keys imported.");

        // Everything the catalog lists is something it claims can be imported. An entry that cannot
        // is a listing bug, not a layout the distribution shipped broken.
        Assert.Empty(failed);
        Assert.Empty(unmappable);
        Assert.Empty(invalid);
        Assert.True(descriptors.Count > 500, $"Expected a full catalog; got {descriptors.Count} entries.");
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task ImportAsync_ForALayoutWithNoVariant_ImportsItRatherThanFailing()
    {
        if (!TryCreateSource(out var source))
        {
            return;
        }

        // The bare layout is most of the catalog, and it resolves to the file's `default` section
        // rather than to a section named after the word.
        var result = await source.ImportAsync(
            new ImportableLayoutReference("linux-xkb", "us", VariantId: null, $"{RootDirectory}/symbols/us"),
            LayoutImportOptions.Default);

        Assert.True(result.Success);
        Assert.Equal("ansi-104", result.SuggestedTemplateId);
        Assert.NotEmpty(result.Project!.Keyboard.Keys);

        var q = Assert.Single(result.Project.Layout.Mappings, mapping => mapping.KeyId == "KeyQ");
        Assert.Equal(LogicalKey.Q, q.LogicalKey);
        Assert.Equal(new CharacterOutput("q"), q.Outputs[ModifierLayer.Default]);
        Assert.Equal(new CharacterOutput("Q"), q.Outputs[ModifierLayer.Shift]);
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task ImportAsync_ForDvorak_KeepsEachKeysPhysicalIdentity()
    {
        if (!TryCreateSource(out var source))
        {
            return;
        }

        var result = await source.ImportAsync(
            new ImportableLayoutReference("linux-xkb", "us", "dvorak", $"{RootDirectory}/symbols/us"),
            LayoutImportOptions.Default);

        Assert.True(result.Success);

        // Dvorak types an apostrophe where QWERTY types Q. The mapping records the key that was
        // pressed; the output records what it types.
        var key = Assert.Single(result.Project!.Layout.Mappings, mapping => mapping.KeyId == "KeyQ");
        Assert.Equal(LogicalKey.Q, key.LogicalKey);
        Assert.Equal(new CharacterOutput("'"), key.Outputs[ModifierLayer.Default]);
    }

    private static bool TryCreateSource(out XkbLayoutImportSource source)
    {
        var fileSystem = new HostXkbFileSystem();

        if (OperatingSystem.IsLinux() && fileSystem.DirectoryExists($"{RootDirectory}/symbols"))
        {
            XkbDataRoot[] roots = [new(RootDirectory, LayoutSourceOrigin.System)];

            source = new XkbLayoutImportSource(
                fileSystem,
                new StaticDataRootLocator(roots),
                new XkbRulesRegistryReader(fileSystem),
                new XkbSymbolsResolver(fileSystem, new XkbIncludeResolver(fileSystem, roots)),
                new XkbKeyNameMapper(),
                new XkbKeysymDecoder(),
                new KeyboardTemplateProvider());

            return true;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            && OperatingSystem.IsLinux())
        {
            Assert.Fail($"'{RootDirectory}' is required for XkbIntegration tests in Linux CI.");
        }

        source = null!;
        return false;
    }

    private static string Describe(ImportableLayoutDescriptor descriptor) =>
        descriptor.VariantId is null
            ? descriptor.LayoutId
            : $"{descriptor.LayoutId}({descriptor.VariantId})";

    private sealed class StaticDataRootLocator(IReadOnlyList<XkbDataRoot> roots) : IXkbDataRootLocator
    {
        public IReadOnlyList<XkbDataRoot> Locate() => roots;
    }
}
