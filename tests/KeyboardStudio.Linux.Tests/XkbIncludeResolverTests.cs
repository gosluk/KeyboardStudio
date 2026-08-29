using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbIncludeResolverTests
{
    private const string SystemRoot = "/usr/share/X11/xkb";

    private static XkbIncludeResolver Resolver(FakeXkbFileSystem fileSystem) =>
        new(fileSystem, [new XkbDataRoot(SystemRoot, LayoutSourceOrigin.System)]);

    private static IReadOnlyList<XkbIncludeSpec> Parse(string specification) =>
        Resolver(new FakeXkbFileSystem()).Parse(specification, XkbMergeMode.Default);

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForABareFileName_LeavesTheSectionUnnamedSoTheDefaultOneIsUsed()
    {
        var spec = Assert.Single(Parse("us"));

        Assert.Equal("us", spec.File);
        Assert.Null(spec.Section);
        Assert.Equal(1, spec.Group);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAFileAndSection_ReadsBoth()
    {
        var spec = Assert.Single(Parse("us(basic)"));

        Assert.Equal("us", spec.File);
        Assert.Equal("basic", spec.Section);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForASubdirectoryForm_KeepsThePathIntact()
    {
        var spec = Assert.Single(Parse("sun_vndr/us(sun_type6)"));

        Assert.Equal("sun_vndr/us", spec.File);
        Assert.Equal("sun_type6", spec.Section);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForSeveralIncludesJoinedByPlus_GivesEachTheOverrideRuleThatSeparatorMeans()
    {
        var specs = Parse("us(basic)+de(nodeadkeys)");

        Assert.Equal(2, specs.Count);
        Assert.Equal("us", specs[0].File);
        Assert.Equal("de", specs[1].File);
        Assert.Equal(XkbMergeMode.Override, specs[1].Merge);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAnIncludeJoinedByBar_GivesItAugmentSoTheEarlierDefinitionStands()
    {
        var specs = Parse("us(basic)|de(basic)");

        Assert.Equal(XkbMergeMode.Augment, specs[1].Merge);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForTheFirstPiece_KeepsTheRuleTheStatementItselfDeclared()
    {
        var specs = Resolver(new FakeXkbFileSystem()).Parse("us(basic)+de", XkbMergeMode.Augment);

        Assert.Equal(XkbMergeMode.Augment, specs[0].Merge);
        Assert.Equal(XkbMergeMode.Override, specs[1].Merge);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAGroupSuffix_ReadsTheTargetGroup()
    {
        var spec = Assert.Single(Parse("ru(basic):2"));

        Assert.Equal("ru", spec.File);
        Assert.Equal("basic", spec.Section);
        Assert.Equal(2, spec.Group);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAnEmptySpecification_YieldsNothingRatherThanAnUnnamedFile()
    {
        Assert.Empty(Parse(string.Empty));
        Assert.Empty(Parse("  "));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAnUnclosedSection_ReadsTheIntentInsteadOfDiscardingTheInclude()
    {
        var spec = Assert.Single(Parse("us(basic"));

        Assert.Equal("us", spec.File);
        Assert.Equal("basic", spec.Section);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveFilePath_WhenARootHoldsTheFile_ReturnsItsPath()
    {
        var fileSystem = new FakeXkbFileSystem().AddFile($"{SystemRoot}/symbols/us", string.Empty);

        Assert.Equal(
            $"{SystemRoot}/symbols/us",
            Resolver(fileSystem).ResolveFilePath("us"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveFilePath_ForASubdirectoryForm_LooksUnderThatSubdirectory()
    {
        var fileSystem = new FakeXkbFileSystem()
            .AddFile($"{SystemRoot}/symbols/sun_vndr/us", string.Empty);

        Assert.Equal(
            $"{SystemRoot}/symbols/sun_vndr/us",
            Resolver(fileSystem).ResolveFilePath("sun_vndr/us"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveFilePath_WhenTwoRootsHoldTheName_TakesTheEarlierRoot()
    {
        const string UserRoot = "/home/someone/.config/xkb";
        var fileSystem = new FakeXkbFileSystem()
            .AddFile($"{UserRoot}/symbols/us", string.Empty)
            .AddFile($"{SystemRoot}/symbols/us", string.Empty);

        var resolver = new XkbIncludeResolver(
            fileSystem,
            [new XkbDataRoot(UserRoot, LayoutSourceOrigin.User), new XkbDataRoot(SystemRoot, LayoutSourceOrigin.System)]);

        Assert.Equal($"{UserRoot}/symbols/us", resolver.ResolveFilePath("us"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveFilePath_WhenNoRootHoldsTheFile_ReturnsNull()
    {
        Assert.Null(Resolver(new FakeXkbFileSystem()).ResolveFilePath("nonexistent"));
    }
}
