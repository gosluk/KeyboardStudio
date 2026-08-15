using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbKeyNameMapperTests
{
    [Theory]
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

    [Fact]
    public void Map_UnknownPair_ReturnsStableKeyLinkedDiagnostic()
    {
        var result = new XkbKeyNameMapper().Map("iso-105", "VendorKey");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(XkbKeyNameMapper.UnsupportedPhysicalKeyCode, diagnostic.Code);
        Assert.Equal("VendorKey", diagnostic.KeyId);
    }
}
