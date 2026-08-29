using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// The seam between the host's XKB database and the import contract: what the catalog lists, and
/// which section an entry actually resolves to.
/// </summary>
public sealed class XkbLayoutImportSourceTests
{
    private const string SystemRoot = "/usr/share/X11/xkb";
    private const string UserRoot = "/home/user/.config/xkb";

    private const string Registry = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xkbConfigRegistry version="1.1">
          <layoutList>
            <layout>
              <configItem>
                <name>us</name>
                <description>English (US)</description>
                <countryList><iso3166Id>US</iso3166Id></countryList>
              </configItem>
              <variantList>
                <variant>
                  <configItem><name>dvorak</name><description>English (Dvorak)</description></configItem>
                </variant>
              </variantList>
            </layout>
            <layout>
              <configItem>
                <name>de</name>
                <description>German</description>
                <countryList><iso3166Id>DE</iso3166Id></countryList>
              </configItem>
            </layout>
          </layoutList>
        </xkbConfigRegistry>
        """;

    // The `default` flag names the section; the word is never a section name in its own right.
    private const string UsSymbols = """
        default partial alphanumeric_keys
        xkb_symbols "basic" {
            name[Group1] = "English (US)";
            key <AD01> { [ q, Q ] };
            key <AD02> { [ w, W ] };
        };

        partial alphanumeric_keys
        xkb_symbols "dvorak" {
            name[Group1] = "English (Dvorak)";
            key <AD01> { [ apostrophe, quotedbl ] };
        };
        """;

    private const string DeSymbols = """
        default partial alphanumeric_keys
        xkb_symbols "basic" {
            name[Group1] = "German";
            key <LatZ> { [ z, Z ] };
        };
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_ForALayoutWithNoVariant_ImportsTheFilesDefaultSection()
    {
        // A null variant means the section flagged `default`, which is named "basic" here and is
        // never named "default" anywhere. Passing the word through as a section name finds nothing
        // and fails every bare layout in the catalog — which is most of it.
        var result = await CreateSource().ImportAsync(
            new ImportableLayoutReference("linux-xkb", "us", VariantId: null, $"{SystemRoot}/symbols/us"),
            LayoutImportOptions.Default);

        Assert.True(result.Success);
        Assert.Equal("English (US)", result.Project!.Metadata.Name);
        Assert.Equal(2, result.Report.KeysImported);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_ForAVariant_ImportsThatSection()
    {
        var result = await CreateSource().ImportAsync(
            new ImportableLayoutReference("linux-xkb", "us", "dvorak", $"{SystemRoot}/symbols/us"),
            LayoutImportOptions.Default);

        var mapping = Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal("KeyQ", mapping.KeyId);
        Assert.Equal(new CharacterOutput("'"), mapping.Outputs[ModifierLayer.Default]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_ForAQwertzLayout_ReadsPhoneticAliasesTheWayTheHostWould()
    {
        // <LatZ> is defined three times over in keycodes/aliases and rules/evdev picks the set from
        // the layout. For a German layout it is the top-row key, not the bottom-row one, and
        // reading it with the default set returns the layout with Y and Z transposed.
        var result = await CreateSource().ImportAsync(
            new ImportableLayoutReference("linux-xkb", "de", VariantId: null, $"{SystemRoot}/symbols/de"),
            LayoutImportOptions.Default);

        Assert.Equal("KeyY", Assert.Single(result.Project!.Layout.Mappings).KeyId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ListAsync_ForALayoutTheRegistryDoesNotDescribe_ListsItUnderItsFileName()
    {
        var descriptors = await CreateSource().ListAsync();

        var custom = Assert.Single(descriptors, descriptor => descriptor.LayoutId == "mine");
        Assert.Null(custom.VariantId);
        Assert.Equal("mine", custom.DisplayName);
        Assert.Empty(custom.Countries);
        Assert.Equal($"{UserRoot}/symbols/mine", custom.SourceLocation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_ForALayoutTheRegistryDoesNotDescribe_SaysSoAndStillImportsIt()
    {
        var result = await CreateSource().ImportAsync(
            new ImportableLayoutReference("linux-xkb", "mine", VariantId: null, $"{UserRoot}/symbols/mine"),
            LayoutImportOptions.Default);

        Assert.True(result.Success);
        Assert.Contains(
            result.Report.Diagnostics,
            item => item.Code == LayoutImportDiagnosticCodes.LayoutMetadataUnavailable
                && item.Severity == ValidationSeverity.Info);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ListAsync_ForEachEntry_TakesItsOriginFromTheRootThatDefinedIt()
    {
        // The origin is what lets the catalog separate the user's own layouts from the
        // distribution's, and it is a property of the root rather than of the entry.
        var descriptors = await CreateSource().ListAsync();

        Assert.Equal(LayoutSourceOrigin.System, Single(descriptors, "us").Origin);
        Assert.Equal(LayoutSourceOrigin.User, Single(descriptors, "mine").Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ListAsync_ForALayoutTwoRootsDefine_KeepsTheOneThatWins()
    {
        // libxkbcommon reads the roots in order and the first definition wins, so the catalog must
        // not offer the shadowed copy as a second entry.
        var fileSystem = FileSystem().AddFile($"{UserRoot}/symbols/us", UsSymbols);

        var descriptors = await CreateSource(fileSystem).ListAsync();

        var us = Single(descriptors, "us");
        Assert.Equal(LayoutSourceOrigin.System, us.Origin);
        Assert.Equal($"{SystemRoot}/symbols/us", us.SourceLocation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportAsync_ForALayoutNoRootDefines_FailsWithoutThrowing()
    {
        var result = await CreateSource().ImportAsync(
            new ImportableLayoutReference("linux-xkb", "nowhere", "basic", "/nowhere"),
            LayoutImportOptions.Default);

        Assert.False(result.Success);
        Assert.Contains(
            result.Report.Diagnostics,
            item => item.Code == LayoutImportDiagnosticCodes.CompositionTargetUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsAvailable_WhenNoRootHoldsSymbols_IsFalse()
    {
        // A root with rules but no symbols has nothing importable in it.
        var fileSystem = new FakeXkbFileSystem().AddFile($"{SystemRoot}/rules/evdev.xml", Registry);

        Assert.False(CreateSource(fileSystem).IsAvailable);
        Assert.True(CreateSource().IsAvailable);
    }

    private static ImportableLayoutDescriptor Single(
        IReadOnlyList<ImportableLayoutDescriptor> descriptors,
        string layoutId) =>
        Assert.Single(descriptors, descriptor => descriptor.LayoutId == layoutId && descriptor.VariantId is null);

    private static FakeXkbFileSystem FileSystem() =>
        new FakeXkbFileSystem()
            .AddFile($"{SystemRoot}/rules/evdev.xml", Registry)
            .AddFile($"{SystemRoot}/symbols/us", UsSymbols)
            .AddFile($"{SystemRoot}/symbols/de", DeSymbols)
            .AddFile($"{UserRoot}/symbols/mine", UsSymbols);

    private static XkbLayoutImportSource CreateSource(FakeXkbFileSystem? fileSystem = null)
    {
        fileSystem ??= FileSystem();

        XkbDataRoot[] roots =
        [
            new(SystemRoot, LayoutSourceOrigin.System),
            new(UserRoot, LayoutSourceOrigin.User)
        ];

        return new XkbLayoutImportSource(
            fileSystem,
            new FakeXkbDataRootLocator(roots),
            new XkbRulesRegistryReader(fileSystem),
            new XkbSymbolsResolver(fileSystem, new XkbIncludeResolver(fileSystem, roots)),
            new XkbKeyNameMapper(),
            new XkbKeysymDecoder(),
            new KeyboardTemplateProvider());
    }

    private sealed class FakeXkbDataRootLocator(IReadOnlyList<XkbDataRoot> roots) : IXkbDataRootLocator
    {
        public IReadOnlyList<XkbDataRoot> Locate() => roots;
    }
}
