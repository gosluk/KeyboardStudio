using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class SeedProjectTranslationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_ForTheUsBasicSeed_ProducesEveryKeyWithoutDiagnostics()
    {
        var project = new EmbeddedSeedProjectSource().Create(SeedProjectId.UsBasic);

        var result = new XkbLayoutTranslator().Translate(
            project,
            new XkbLayoutMetadata("us-custom", "basic", "Seed"));

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Layout);
        Assert.Equal(project.Layout.Mappings.Count, result.Layout.Mappings.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_ForTheUsBasicSeed_EmitsTheExpectedKeysyms()
    {
        var project = new EmbeddedSeedProjectSource().Create(SeedProjectId.UsBasic);

        var result = new XkbLayoutTranslator().Translate(
            project,
            new XkbLayoutMetadata("us-custom", "basic", "Seed"));

        var byKeyName = result.Layout!.Mappings.ToDictionary(
            mapping => mapping.KeyName,
            StringComparer.Ordinal);

        Assert.Equal(["a", "A"], byKeyName["<AC01>"].Keysyms);
        Assert.Equal(["1", "exclam"], byKeyName["<AE01>"].Keysyms);
        Assert.Equal(["backslash", "bar"], byKeyName["<BKSL>"].Keysyms);
        Assert.Equal(["less", "greater"], byKeyName["<LSGT>"].Keysyms);
        Assert.Equal(["Return"], byKeyName["<RTRN>"].Keysyms);
    }
}
