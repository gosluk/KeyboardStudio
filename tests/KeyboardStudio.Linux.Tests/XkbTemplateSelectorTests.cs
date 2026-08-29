using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// The geometry suggestion. Nothing in the XKB database records which keyboard a layout was drawn
/// for, so every case here is an inference the user is allowed to overrule.
/// </summary>
public sealed class XkbTemplateSelectorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void SelectTemplate_WhenTheLayoutWritesTheIsoOnlyKey_SuggestsIso105()
    {
        // <LSGT> is the key ANSI boards do not have. A layout that maps it needs a board with it,
        // whatever the registry says about where the layout is used.
        var symbols = Symbols(Key("<LSGT>", "less", "greater"));

        Assert.Equal("iso-105", XkbTemplateSelector.SelectTemplate(symbols, Registry("us", "US")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectTemplate_ForAnAnsiCountry_SuggestsAnsi104()
    {
        Assert.Equal("ansi-104", XkbTemplateSelector.SelectTemplate(Symbols(), Registry("us", "US")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectTemplate_ForAnIsoCountry_SuggestsIso105()
    {
        Assert.Equal("iso-105", XkbTemplateSelector.SelectTemplate(Symbols(), Registry("pl", "PL")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectTemplate_ForALayoutServingAnsiAndIsoCountriesAlike_SuggestsIso105()
    {
        // ISO is the board that can hold both, so a layout shared across the divide is offered it
        // rather than the one that would silently lose a key.
        var registry = Registry("es", "US", "ES");

        Assert.Equal("iso-105", XkbTemplateSelector.SelectTemplate(Symbols(), registry));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectTemplate_ForALayoutTheRegistryDoesNotDescribe_SuggestsIso105()
    {
        Assert.Equal("iso-105", XkbTemplateSelector.SelectTemplate(Symbols(), registryEntry: null));
    }

    private static ResolvedXkbSymbols Symbols(params ResolvedXkbKey[] keys) =>
        new("/xkb/symbols/test", "basic", "Test", keys, ["test(basic)"], []);

    private static ResolvedXkbKey Key(string name, params string[] keysyms) =>
        new(name, keysyms, "test(basic)");

    private static XkbRegistryEntry Registry(string layoutId, params string[] countries) =>
        new(layoutId, VariantId: null, layoutId, ShortDescription: null, Languages: [], countries);
}
