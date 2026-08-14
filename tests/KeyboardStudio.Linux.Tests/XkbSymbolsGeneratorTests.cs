using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbSymbolsGeneratorTests
{
    [Fact]
    public void Generate_SortsMappingsAndEmitsRequiredTypesAndLevelThreeInclude()
    {
        var layout = new XkbKeyboardLayout(
            new XkbLayoutMetadata("demo", "basic", "Demo \"AltGr\""),
            [
                new XkbKeyMapping("KeyB", "<AB05>", XkbKeyType.TwoLevel, ["b", "B"]),
                new XkbKeyMapping(
                    "KeyA",
                    "<AC01>",
                    XkbKeyType.FourLevelAlphabetic,
                    ["a", "A", "U0105", "U0104"])
            ],
            true);

        var generated = new XkbSymbolsGenerator().Generate(layout);

        Assert.Equal(Path.Combine("symbols", "demo"), generated.RelativePath);
        Assert.True(
            generated.Content.IndexOf("<AB05>", StringComparison.Ordinal) <
            generated.Content.IndexOf("<AC01>", StringComparison.Ordinal));
        Assert.Contains("type[Group1] = \"FOUR_LEVEL_ALPHABETIC\"", generated.Content);
        Assert.Contains("include \"level3(ralt_switch)\"", generated.Content);
        Assert.Contains("name[Group1] = \"Demo \\\"AltGr\\\"\";", generated.Content);
        Assert.DoesNotContain('\r', generated.Content);
    }

    [Fact]
    public void Generate_SameLayoutTwice_IsByteForByteDeterministic()
    {
        var layout = new XkbKeyboardLayout(
            new XkbLayoutMetadata("demo", "basic", "Demo"),
            [new XkbKeyMapping("Enter", "<RTRN>", XkbKeyType.OneLevel, ["Return"])],
            false);
        var generator = new XkbSymbolsGenerator();

        Assert.Equal(generator.Generate(layout).Content, generator.Generate(layout).Content);
    }
}
