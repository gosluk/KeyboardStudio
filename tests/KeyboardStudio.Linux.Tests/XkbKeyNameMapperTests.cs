using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbKeyNameMapperTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("iso-105", "KeyA", "<AC01>")]
    [InlineData("ansi-104", "Digit1", "<AE01>")]
    [InlineData("iso-105", "IntlBackslash", "<LSGT>")]
    [InlineData("ansi-104", "Enter", "<RTRN>")]
    [InlineData("iso-105", "NumpadEnter", "<KPEN>")]
    public void Map_KnownPhysicalIdentity_ReturnsXkbKeyName(
        string templateId,
        string keyId,
        string expected)
    {
        var result = new XkbKeyNameMapper().Map(templateId, keyId);

        Assert.True(result.Success);
        Assert.Equal(expected, result.KeyName);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("iso-105")]
    [InlineData("ansi-104")]
    public void Map_TemplateKeys_HasCompleteCoverage(string templateId)
    {
        var keyboard = new KeyboardTemplateProvider().Load(templateId);
        var mapper = new XkbKeyNameMapper();

        var failures = keyboard.Keys
            .Select(key => mapper.Map(templateId, key.Id))
            .Where(result => !result.Success)
            .ToArray();

        Assert.Empty(failures);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("iso-105")]
    [InlineData("ansi-104")]
    public void GetMappings_ForATemplate_AgreesWithMapForEveryKeyOfIt(string templateId)
    {
        // The table and the single lookup have to be the same data, because import reads the table
        // and generation reads the lookup. A table that had drifted would move keys on the way in
        // and no test of either direction alone would notice.
        var mapper = new XkbKeyNameMapper();
        var keyboard = new KeyboardTemplateProvider().Load(templateId);
        var mappings = mapper.GetMappings(templateId);

        Assert.Equal(keyboard.Keys.Count, mappings.Count);

        foreach (var key in keyboard.Keys)
        {
            Assert.Equal(mapper.Map(templateId, key.Id).KeyName, mappings[key.Id]);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMappings_ForTheTwoTemplates_DiffersWhereTheKeyboardsDo()
    {
        var mapper = new XkbKeyNameMapper();

        var iso = mapper.GetMappings("iso-105");
        var ansi = mapper.GetMappings("ansi-104");

        Assert.Equal("<LSGT>", iso["IntlBackslash"]);
        Assert.DoesNotContain("IntlBackslash", ansi.Keys);
        Assert.Equal("<BKSL>", iso["IntlHash"]);
        Assert.Equal("<BKSL>", ansi["Backslash"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMappings_ForATemplateWithNoTable_IsEmptyRatherThanThrowing()
    {
        Assert.Empty(new XkbKeyNameMapper().GetMappings("no-such-template"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Map_UnknownPair_ReturnsStableKeyLinkedDiagnostic()
    {
        var result = new XkbKeyNameMapper().Map("iso-105", "VendorKey");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(XkbKeyNameMapper.UnsupportedPhysicalKeyCode, diagnostic.Code);
        Assert.Equal("VendorKey", diagnostic.KeyId);
    }
}
