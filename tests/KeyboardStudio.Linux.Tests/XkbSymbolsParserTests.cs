using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbSymbolsParserTests
{
    private static XkbSymbolsFile Parse(string text) =>
        new XkbSymbolsParser().Parse("/usr/share/X11/xkb/symbols/pl", text);

    private static XkbSymbolsSection ParseSingleSection(string body) =>
        Assert.Single(Parse($$"""
            default partial alphanumeric_keys
            xkb_symbols "basic" {
            {{body}}
            };
            """).Sections);

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForASectionHeader_ReadsItsNameAndTheFlagsThatChangeSelection()
    {
        var file = Parse("""
            hidden partial alphanumeric_keys
            xkb_symbols "inet" {
            };
            """);

        var section = Assert.Single(file.Sections);
        Assert.Equal("inet", section.Name);
        Assert.True(section.IsHidden);
        Assert.True(section.IsPartial);
        Assert.False(section.IsDefault);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAFileOfSeveralSections_KeepsThemAllInSourceOrder()
    {
        // An include names one section of a file; `pl` alone defines a dozen.
        var file = Parse("""
            default partial xkb_symbols "basic" { };
            partial xkb_symbols "legacy" { };
            partial xkb_symbols "qwertz" { };
            """);

        Assert.Equal(["basic", "legacy", "qwertz"], file.Sections.Select(section => section.Name));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAKeyStatement_ReadsItsNameAndLevelsInOrder()
    {
        var section = ParseSingleSection("key <AC01> { [ a, A, aogonek, Aogonek ] };");

        var key = Assert.IsType<XkbKeyStatement>(Assert.Single(section.Statements));
        Assert.Equal("<AC01>", key.KeyName);
        Assert.Equal(["a", "A", "aogonek", "Aogonek"], key.Keysyms);
        Assert.Equal(XkbMergeMode.Default, key.Merge);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAnExplicitSymbolsAssignment_ReadsItTheSameAsABareList()
    {
        var section = ParseSingleSection("""
            key <AC01> { type[Group1] = "FOUR_LEVEL", symbols[Group1] = [ a, A, aogonek, Aogonek ] };
            """);

        var key = Assert.IsType<XkbKeyStatement>(Assert.Single(section.Statements));
        Assert.Equal(["a", "A", "aogonek", "Aogonek"], key.Keysyms);
        Assert.Empty(section.Statements.OfType<XkbIgnoredStatement>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAKeyTypeInsideAKey_ReadsItWithoutRaisingAnything()
    {
        // A key type cannot change which character a level produces, so dropping it costs nothing
        // and a finding about it would be noise in every import.
        var file = Parse("""
            xkb_symbols "basic" {
              key <AE01> { type = "FOUR_LEVEL", [ 1, exclam ] };
            };
            """);

        Assert.Empty(file.Diagnostics);
        Assert.Equal(["1", "exclam"], Assert.IsType<XkbKeyStatement>(file.Sections[0].Statements[0]).Keysyms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForSeveralGroupsOnOneKey_KeepsTheFirstAndReportsTheRest()
    {
        var file = Parse("""
            xkb_symbols "basic" {
              key <TLDE> { [ grave, asciitilde ], [ x, X ] };
            };
            """);

        var key = Assert.IsType<XkbKeyStatement>(file.Sections[0].Statements[0]);
        Assert.Equal(["grave", "asciitilde"], key.Keysyms);

        var diagnostic = Assert.Single(file.Diagnostics);
        Assert.Equal(LayoutImportDiagnosticCodes.AlternateGroupsIgnored, diagnostic.Code);
        Assert.Equal(ValidationSeverity.Warning, diagnostic.Severity);
        Assert.Contains("<TLDE>", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal("<TLDE>", diagnostic.SourceKeyName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForASecondGroupAssignedByName_ReportsItTheSameWay()
    {
        var file = Parse("""
            xkb_symbols "basic" {
              key <TLDE> { symbols[Group1] = [ grave ], symbols[Group2] = [ x ] };
            };
            """);

        Assert.Equal(["grave"], Assert.IsType<XkbKeyStatement>(file.Sections[0].Statements[0]).Keysyms);
        Assert.Equal(LayoutImportDiagnosticCodes.AlternateGroupsIgnored, Assert.Single(file.Diagnostics).Code);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAConstructThatWouldChangeBehavior_DropsItWithAWarning()
    {
        // Actions, overlays, and redirects all change what a key does, so silence would misrepresent
        // the import as faithful.
        var file = Parse("""
            xkb_symbols "basic" {
              key <SPCE> { [ space ], actions[Group1] = [ SetMods(modifiers=Shift) ] };
            };
            """);

        var diagnostic = Assert.Single(file.Diagnostics);
        Assert.Equal(LayoutImportDiagnosticCodes.UnsupportedConstructIgnored, diagnostic.Code);
        Assert.Equal(ValidationSeverity.Warning, diagnostic.Severity);
        Assert.Equal("<SPCE>", diagnostic.SourceKeyName);
        Assert.Equal(["space"], Assert.IsType<XkbKeyStatement>(file.Sections[0].Statements[0]).Keysyms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAnInclude_KeepsTheSpecificationExactlyAsWritten()
    {
        // One string can name several sections joined by `+`; splitting it is the resolver's job.
        var section = ParseSingleSection("""
            include "latin(type4)"
            augment include "level3(ralt_switch)"
            """);

        Assert.Equal(
            [
                (XkbMergeMode.Default, "latin(type4)"),
                (XkbMergeMode.Augment, "level3(ralt_switch)")
            ],
            section.Statements.OfType<XkbIncludeStatement>()
                .Select(include => (include.Merge, include.Specification)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAMergePrefixOnAKey_RecordsItForTheResolver()
    {
        var section = ParseSingleSection("""
            replace key <AD01> { [ q ] };
            override key <AD02> { [ w ] };
            """);

        Assert.Equal(
            [XkbMergeMode.Replace, XkbMergeMode.Override],
            section.Statements.OfType<XkbKeyStatement>().Select(key => key.Merge));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForANameStatement_ReadsTheGroupAndTheValue()
    {
        var section = ParseSingleSection("""name[Group1] = "Polish (legacy)";""");

        var name = Assert.IsType<XkbNameStatement>(Assert.Single(section.Statements));
        Assert.Equal(1, name.Group);
        Assert.Equal("Polish (legacy)", name.Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForStatementsThatCannotAffectAnOutput_RecordsThemAsIgnoredWithoutADiagnostic()
    {
        // Recognized-and-irrelevant must stay distinguishable from not-understood: conflating them
        // would either bury real gaps in noise or hide them entirely.
        var file = Parse("""
            xkb_symbols "basic" {
              key.type[group1] = "ALPHABETIC";
              modifier_map Shift { Shift_L, Shift_R };
              virtual_modifiers LevelThree;
            };
            """);

        Assert.Empty(file.Diagnostics);
        Assert.Equal(
            ["key.type", "modifier_map", "virtual_modifiers"],
            file.Sections[0].Statements.OfType<XkbIgnoredStatement>().Select(statement => statement.Keyword));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("virtualMods")]
    [InlineData("vmods")]
    public void Parse_ForAKeyBindingAVirtualModifier_ReadsItsSymbolsAndSaysNothing(string property)
    {
        // XKB accepts both spellings of the property and xkeyboard-config writes both — the
        // abbreviation in symbols/level5. Neither changes what the key types, so neither is worth
        // a diagnostic, but an unknown one would cost the key its symbols.
        var section = ParseSingleSection($$"""
              replace key <HYPR> { [ NoSymbol ], type[group1] = "ONE_LEVEL", {{property}} = NumLock };
            """);

        var key = Assert.Single(section.Statements.OfType<XkbKeyStatement>());
        Assert.Equal("<HYPR>", key.KeyName);
        Assert.Equal(["NoSymbol"], key.Keysyms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Parse_ForAStatementItDoesNotKnow_SkipsToTheNextSemicolonAndCarriesOn()
    {
        // The goal is a usable starting point, not a conformant compiler: one unknown statement
        // must not cost the keys around it.
        var file = Parse("""
            xkb_symbols "basic" {
              key <AD01> { [ q ] };
              alias <MENU> = <COMP>;
              key <AD02> { [ w ] };
            };
            """);

        Assert.Equal(
            ["<AD01>", "<AD02>"],
            file.Sections[0].Statements.OfType<XkbKeyStatement>().Select(key => key.KeyName));

        var diagnostic = Assert.Single(file.Diagnostics);
        Assert.Equal(LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped, diagnostic.Code);
        Assert.Equal(ValidationSeverity.Info, diagnostic.Severity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Parse_ForAKeyMissingItsTerminator_StillReadsTheKeyThatFollows()
    {
        var file = Parse("""
            xkb_symbols "basic" {
              key <AD01> { [ q ] }
              key <AD02> { [ w ] };
            };
            """);

        Assert.Equal(
            ["<AD01>", "<AD02>"],
            file.Sections[0].Statements.OfType<XkbKeyStatement>().Select(key => key.KeyName));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultSection_WhenOneIsFlagged_IsTheFlaggedOne()
    {
        var file = Parse("""
            partial xkb_symbols "legacy" { };
            default partial xkb_symbols "basic" { };
            """);

        Assert.Equal("basic", file.DefaultSection?.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultSection_WhenNoneIsFlagged_IsTheFirstAsLibxkbcommonTreatsIt()
    {
        var file = Parse("""
            partial xkb_symbols "legacy" { };
            partial xkb_symbols "basic" { };
            """);

        Assert.Equal("legacy", file.DefaultSection?.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FindSection_ForANameTheFileDoesNotDefine_ReturnsNothing()
    {
        var file = Parse("""default partial xkb_symbols "basic" { };""");

        Assert.Equal("basic", file.FindSection("basic")?.Name);
        Assert.Null(file.FindSection("nonexistent"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForARealisticSection_ReadsEveryStatementItSupports()
    {
        var file = Parse("""
            // Polish keyboard, abbreviated from the real file.
            default partial alphanumeric_keys
            xkb_symbols "basic" {

                include "latin(type4)"

                name[Group1] = "Polish";

                key <AD01>	{ [         q,          Q,  paragraph,  Paragraph ]	};
                key <AC01>	{ [         a,          A,    aogonek,    Aogonek ]	};
                key <AB03>	{ [         c,          C,     cacute,     Cacute ]	};
                key <SPCE>	{ [     space,      space,      space,      space ]	};

                include "level3(ralt_switch)"
            };
            """);

        var section = Assert.Single(file.Sections);
        Assert.True(section.IsDefault);
        Assert.Empty(file.Diagnostics);
        Assert.Equal(
            ["latin(type4)", "level3(ralt_switch)"],
            section.Statements.OfType<XkbIncludeStatement>().Select(include => include.Specification));
        Assert.Equal("Polish", section.Statements.OfType<XkbNameStatement>().Single().Value);
        Assert.Equal(
            ["<AD01>", "<AC01>", "<AB03>", "<SPCE>"],
            section.Statements.OfType<XkbKeyStatement>().Select(key => key.KeyName));
        Assert.Equal(
            ["c", "C", "cacute", "Cacute"],
            section.Statements.OfType<XkbKeyStatement>().Single(key => key.KeyName == "<AB03>").Keysyms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Parse_ForATruncatedFile_ReturnsWhatItReadRatherThanThrowing()
    {
        var file = Parse("""
            xkb_symbols "basic" {
              key <AD01> { [ q, Q ] };
              key <AD02> { [ w,
            """);

        Assert.Equal(
            ["<AD01>", "<AD02>"],
            file.Sections[0].Statements.OfType<XkbKeyStatement>().Select(key => key.KeyName));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Parse_ForAMergeKeywordUsedInPlaceOfInclude_ReadsItAsAnIncludeWithThatRule()
    {
        // `augment "us(basic)"` has no `include` keyword at all: the merge rule is the keyword.
        var section = ParseSingleSection("""
            augment "us(basic)"
            """);

        var include = Assert.IsType<XkbIncludeStatement>(Assert.Single(section.Statements));
        Assert.Equal(XkbMergeMode.Augment, include.Merge);
        Assert.Equal("us(basic)", include.Specification);
    }
}
