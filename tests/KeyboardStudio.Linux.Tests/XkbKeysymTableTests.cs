using System.Text;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Guards the generated keysym table. The file is machine-written from pinned upstream sources, so
/// these are not tests of hand-written logic: they pin the properties an upstream bump could quietly
/// break, and the shape the decoder relies on.
/// </summary>
public sealed class XkbKeysymTableTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("a", 0x0061u, 0x0061)]
    [InlineData("space", 0x0020u, 0x0020)]
    [InlineData("Aogonek", 0x01A1u, 0x0104)]
    [InlineData("aogonek", 0x01B1u, 0x0105)]
    [InlineData("eacute", 0x00E9u, 0x00E9)]
    [InlineData("EuroSign", 0x20ACu, 0x20AC)]
    [InlineData("Greek_alpha", 0x07E1u, 0x03B1)]
    [InlineData("Cyrillic_a", 0x06C1u, 0x0430)]
    public void All_ForACharacterKeysym_CarriesItsValueAndCodepoint(
        string name,
        uint value,
        int codepoint)
    {
        var entry = XkbKeysymTable.All[name];

        Assert.Equal(value, entry.Value);
        Assert.Equal(codepoint, entry.Codepoint);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("dead_acute")]
    [InlineData("ISO_Level3_Shift")]
    [InlineData("Shift_L")]
    [InlineData("XF86AudioPlay")]
    [InlineData("Multi_key")]
    public void TryGetCodepoint_ForAKeysymNamingNoCharacter_IsKnownButYieldsNothing(string name)
    {
        Assert.True(XkbKeysymTable.IsKnown(name));
        Assert.False(XkbKeysymTable.TryGetCodepoint(name, out _));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsKnown_ForTextThatIsNotAKeysym_IsFalse()
    {
        Assert.False(XkbKeysymTable.IsKnown("not_a_keysym"));
        Assert.False(XkbKeysymTable.IsKnown("U0105"));
        Assert.False(XkbKeysymTable.IsKnown(string.Empty));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("leftanglebracket", 0x27E8)]
    [InlineData("rightanglebracket", 0x27E9)]
    public void All_WhereTheSourcesDisagree_TakesLibxkbcommonsCharacter(string name, int codepoint)
    {
        // keysymdef.h still says U+2329/U+232A, which Unicode has since deprecated. libxkbcommon is
        // the table the user's machine consults, so it decides what they actually type; the
        // generated header lists every such disagreement so a source bump cannot hide a new one.
        Assert.Equal(codepoint, XkbKeysymTable.All[name].Codepoint);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void All_ForKeysymsInTheUnicodeRange_EncodesTheCharacterInTheValue()
    {
        // The rule keysymdef.h lays down for every keysym added since Unicode. The decoder reads
        // numeric keysyms by applying it directly, so the table has to agree with it.
        var offenders = XkbKeysymTable.All
            .Where(entry => entry.Value.Value is >= 0x01000100 and <= 0x0110FFFF)
            .Where(entry => entry.Value.Codepoint != (int)(entry.Value.Value - 0x01000000))
            .Select(entry => entry.Key)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void All_EveryCodepoint_IsAUnicodeScalar()
    {
        var offenders = XkbKeysymTable.All
            .Where(entry => entry.Value.Codepoint != XkbKeysymTable.NoCodepoint)
            .Where(entry => !Rune.IsValid(entry.Value.Codepoint))
            .Select(entry => entry.Key)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void All_IsTheWholeOfEveryVendoredHeader()
    {
        // A drop in the count means the generator stopped matching a form of #define it used to
        // read — a silent failure the per-keysym cases above would not catch. Over half of
        // XF86keysym.h writes its value through a macro, and one bad regex would drop all of it.
        Assert.Equal(2652, XkbKeysymTable.All.Count);
        Assert.Equal(
            1749,
            XkbKeysymTable.All.Count(entry => entry.Value.Codepoint != XkbKeysymTable.NoCodepoint));
    }

    [Theory]
    [Trait("Category", "Unit")]
    // The deprecated spelling and the endorsed one, for each value keysymdef.h names twice.
    [InlineData("quoteright", "apostrophe", 0x0027)]
    [InlineData("quoteleft", "grave", 0x0060)]
    [InlineData("guillemotleft", "guillemetleft", 0x00AB)]
    [InlineData("masculine", "ordmasculine", 0x00BA)]
    [InlineData("guillemotright", "guillemetright", 0x00BB)]
    [InlineData("Eth", "ETH", 0x00D0)]
    [InlineData("Ooblique", "Oslash", 0x00D8)]
    [InlineData("Thorn", "THORN", 0x00DE)]
    [InlineData("ooblique", "oslash", 0x00F8)]
    public void All_ForADeprecatedAlias_CarriesTheSameCharacterAsTheNameItAliases(
        string deprecated,
        string endorsed,
        int codepoint)
    {
        // keysymdef.h annotates only the endorsed name with its character and leaves the alias with
        // a note saying which name replaced it. Both are the same keysym value and produce the same
        // character on the user's machine, so reading the annotation per name rather than per value
        // silently drops « » ' ` and the rest wherever a layout writes the older spelling — which
        // xkeyboard-config still does.
        Assert.Equal(XkbKeysymTable.All[deprecated].Value, XkbKeysymTable.All[endorsed].Value);
        Assert.Equal(codepoint, XkbKeysymTable.All[deprecated].Codepoint);
        Assert.Equal(codepoint, XkbKeysymTable.All[endorsed].Codepoint);
    }
}
