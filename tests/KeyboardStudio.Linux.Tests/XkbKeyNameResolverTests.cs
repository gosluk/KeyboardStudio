using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbKeyNameResolverTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("iso-105", "<AC01>", "KeyA")]
    [InlineData("iso-105", "<LSGT>", "IntlBackslash")]
    [InlineData("iso-105", "<BKSL>", "IntlHash")]
    [InlineData("ansi-104", "<BKSL>", "Backslash")]
    [InlineData("ansi-104", "<AE01>", "Digit1")]
    [InlineData("ansi-104", "<KPEN>", "NumpadEnter")]
    public void Resolve_KnownKeyName_ReturnsPhysicalKey(string templateId, string keyName, string expected)
    {
        var result = new XkbKeyNameResolver().Resolve(templateId, keyName);

        Assert.True(result.Resolved);
        Assert.Equal(expected, result.KeyId);
        Assert.Null(result.Diagnostic);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("iso-105")]
    [InlineData("ansi-104")]
    public void Resolve_EveryNameGenerationWrites_ReturnsTheKeyItWasWrittenFor(string templateId)
    {
        // The round trip is the point of deriving both directions from one table: a name generation
        // writes for a key must come back as that same key, or a layout that left the editor as
        // <AC01> would return on a different key than it started on.
        var mapper = new XkbKeyNameMapper();
        var resolver = new XkbKeyNameResolver(mapper);

        foreach (var (keyId, keyName) in mapper.GetMappings(templateId))
        {
            var result = resolver.Resolve(templateId, keyName);

            Assert.True(result.Resolved, $"'{keyName}' did not resolve on '{templateId}'.");
            Assert.Equal(keyId, result.KeyId);
        }
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("<AC12>", "<BKSL>")]
    [InlineData("<ALGR>", "<RALT>")]
    [InlineData("<HZTG>", "<TLDE>")]
    [InlineData("<LMTA>", "<LWIN>")]
    [InlineData("<RMTA>", "<RWIN>")]
    [InlineData("<COMP>", "<MENU>")]
    [InlineData("<I127>", "<PAUS>")]
    public void Resolve_AKeycodeAlias_LandsOnTheSameKeyAsTheNameItAliases(string alias, string primary)
    {
        var resolver = new XkbKeyNameResolver();

        var aliased = resolver.Resolve("iso-105", alias);

        Assert.True(aliased.Resolved, $"'{alias}' did not resolve.");
        Assert.Equal(resolver.Resolve("iso-105", primary).KeyId, aliased.KeyId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_AnAliasOfAnAlias_FollowsTheChainToTheKey()
    {
        // <I135> names the compose key, which is <COMP>, which is aliased in turn to <MENU>. Only
        // the last of the three is a name generation writes, so resolving <I135> at all proves the
        // alias pairs are followed to a fixed point rather than one step deep.
        var result = new XkbKeyNameResolver().Resolve("iso-105", "<I135>");

        Assert.Equal("ContextMenu", result.KeyId);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(XkbKeyAliasSet.Qwerty, "<LatZ>", "KeyZ")]
    [InlineData(XkbKeyAliasSet.Qwerty, "<LatY>", "KeyY")]
    [InlineData(XkbKeyAliasSet.Qwerty, "<LatQ>", "KeyQ")]
    [InlineData(XkbKeyAliasSet.Qwertz, "<LatZ>", "KeyY")]
    [InlineData(XkbKeyAliasSet.Qwertz, "<LatY>", "KeyZ")]
    [InlineData(XkbKeyAliasSet.Qwertz, "<LatQ>", "KeyQ")]
    [InlineData(XkbKeyAliasSet.Azerty, "<LatA>", "KeyQ")]
    [InlineData(XkbKeyAliasSet.Azerty, "<LatQ>", "KeyA")]
    [InlineData(XkbKeyAliasSet.Azerty, "<LatM>", "Semicolon")]
    [InlineData(XkbKeyAliasSet.Azerty, "<LatW>", "KeyZ")]
    public void Resolve_APhoneticAlias_FollowsTheAliasSetTheLayoutIsReadWith(
        XkbKeyAliasSet aliasSet,
        string keyName,
        string expected)
    {
        // <LatZ> is the same name on a German keyboard as on a US one and a different physical key,
        // which is why the set is chosen per layout rather than fixed. Getting this wrong swaps Y
        // and Z on every phonetic Russian variant symbols/de defines.
        var result = new XkbKeyNameResolver(new XkbKeyNameMapper(), aliasSet).Resolve("iso-105", keyName);

        Assert.Equal(expected, result.KeyId);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("de", XkbKeyAliasSet.Qwertz)]
    [InlineData("ch", XkbKeyAliasSet.Qwertz)]
    [InlineData("fr", XkbKeyAliasSet.Azerty)]
    [InlineData("be", XkbKeyAliasSet.Azerty)]
    [InlineData("pl", XkbKeyAliasSet.Qwerty)]
    [InlineData("us", XkbKeyAliasSet.Qwerty)]
    public void AliasSetForLayout_FollowsTheRulesFileTheHostReads(string layout, XkbKeyAliasSet expected)
    {
        Assert.Equal(expected, XkbKeyNameResolver.AliasSetForLayout(layout));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("iso-105", "<MUTE>")]
    [InlineData("iso-105", "<I120>")]
    [InlineData("iso-105", "<FK13>")]
    [InlineData("iso-105", "<AB11>")]
    [InlineData("iso-105", "<AE13>")]
    [InlineData("ansi-104", "<LSGT>")]
    public void Resolve_AKeyTheTemplateLacks_SkipsItAsInformation(string templateId, string keyName)
    {
        var result = new XkbKeyNameResolver().Resolve(templateId, keyName);

        Assert.False(result.Resolved);
        Assert.Null(result.KeyId);
        var diagnostic = result.Diagnostic;
        Assert.NotNull(diagnostic);
        Assert.Equal(LayoutImportDiagnosticCodes.PhysicalKeyNotInTemplate, diagnostic.Code);

        // Information rather than a warning: a symbols file naming keys this keyboard does not have
        // is ordinary, and grading it higher would report every ANSI import as degraded.
        Assert.Equal(ValidationSeverity.Info, diagnostic.Severity);
        Assert.Contains(keyName, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAnsi104_DoesNotInventTheKeyIso105HasAndItDoesNot()
    {
        // <LSGT> exists only on the ISO keyboard, and <BKSL> sits on a different physical key on
        // each. Both templates being served from one table is what keeps that straight.
        var resolver = new XkbKeyNameResolver();

        Assert.Equal("IntlBackslash", resolver.Resolve("iso-105", "<LSGT>").KeyId);
        Assert.Null(resolver.Resolve("ansi-104", "<LSGT>").KeyId);
        Assert.Equal("IntlHash", resolver.Resolve("iso-105", "<BKSL>").KeyId);
        Assert.Equal("Backslash", resolver.Resolve("ansi-104", "<BKSL>").KeyId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForATemplateWithNoTable_SkipsEveryKeyRatherThanThrowing()
    {
        var result = new XkbKeyNameResolver().Resolve("no-such-template", "<AE01>");

        Assert.False(result.Resolved);
        Assert.Equal(LayoutImportDiagnosticCodes.PhysicalKeyNotInTemplate, result.Diagnostic!.Code);
    }
}
