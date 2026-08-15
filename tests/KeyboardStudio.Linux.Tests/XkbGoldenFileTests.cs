using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbGoldenFileTests
{
    public static TheoryData<string, XkbKeyboardLayout> Fixtures => new()
    {
        {
            "IsoAltGr.xkb",
            new XkbKeyboardLayout(
                new XkbLayoutMetadata("iso-altgr", "basic", "ISO AltGr"),
                [
                    new XkbKeyMapping(
                        "KeyA",
                        "<AC01>",
                        XkbKeyType.FourLevelAlphabetic,
                        ["a", "A", "U0105", "U0104"])
                ],
                true)
        },
        {
            "AnsiTwoLevel.xkb",
            new XkbKeyboardLayout(
                new XkbLayoutMetadata("ansi-two-level", "basic", "ANSI two-level"),
                [
                    new XkbKeyMapping(
                        "Enter",
                        "<RTRN>",
                        XkbKeyType.OneLevel,
                        ["Return"]),
                    new XkbKeyMapping(
                        "Slash",
                        "<AB10>",
                        XkbKeyType.TwoLevel,
                        ["slash", "question"])
                ],
                false)
        }
    };

    [Theory]
    [Trait("Category", "Golden")]
    [MemberData(nameof(Fixtures))]
    public async Task Generate_RepresentativeLayout_MatchesGoldenFile(
        string fixtureName,
        XkbKeyboardLayout layout)
    {
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Golden", fixtureName);
        var expected = (await File.ReadAllTextAsync(expectedPath)).Replace("\r\n", "\n", StringComparison.Ordinal);

        var actual = new XkbSymbolsGenerator().Generate(layout).Content;

        Assert.Equal(expected, actual);
    }
}
