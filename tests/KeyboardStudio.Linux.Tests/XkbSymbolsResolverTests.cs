using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbSymbolsResolverTests
{
    private const string Root = "/usr/share/X11/xkb";

    private readonly FakeXkbFileSystem _fileSystem = new();

    private void AddSymbols(string name, string content) =>
        _fileSystem.AddFile($"{Root}/symbols/{name}", content);

    private ResolvedXkbSymbols Resolve(string file, string? section = null)
    {
        var resolved = ResolveOrNull(file, section);
        Assert.NotNull(resolved);
        return resolved;
    }

    private ResolvedXkbSymbols? ResolveOrNull(string file, string? section = null) =>
        CreateResolver().Resolve(file, section);

    private ResolvedXkbSymbols ResolveLayout(string file, string? section = null)
    {
        var resolved = CreateResolver().ResolveLayout(file, section);
        Assert.NotNull(resolved);
        return resolved;
    }

    private XkbSymbolsResolver CreateResolver()
    {
        var roots = new[] { new XkbDataRoot(Root, LayoutSourceOrigin.System) };
        return new XkbSymbolsResolver(_fileSystem, new XkbIncludeResolver(_fileSystem, roots));
    }

    /// <summary>A stand-in for the real <c>pc</c>: keys no national layout writes for itself.</summary>
    private void AddCommonBase() =>
        AddSymbols(XkbCommonBase.FileName, """
            default partial alphanumeric_keys modifier_keys
            xkb_symbols "pc105" {
                key <ESC> { [ Escape ] };
                key <LSGT> { [ less, greater ] };
                key <AD01> { [ NoSymbol ] };
            };
            """);

    private static IReadOnlyList<string> KeysymsOf(ResolvedXkbSymbols resolved, string keyName) =>
        Assert.Single(resolved.Keys, key => key.KeyName == keyName).Keysyms;

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForASectionWithNoIncludes_ReturnsItsOwnKeys()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                name[Group1] = "English (US)";
                key <AD01> { [ q, Q ] };
                key <AD02> { [ w, W ] };
            };
            """);

        var resolved = Resolve("us");

        Assert.Equal("basic", resolved.Section);
        Assert.Equal("English (US)", resolved.DisplayName);
        Assert.Equal(["<AD01>", "<AD02>"], resolved.Keys.Select(key => key.KeyName));
        Assert.Equal(["q", "Q"], KeysymsOf(resolved, "<AD01>"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenNoSectionIsNamed_UsesTheOneFlaggedDefault()
    {
        AddSymbols("pl", """
            partial alphanumeric_keys
            xkb_symbols "legacy" {
                key <AD01> { [ x ] };
            };

            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ q ] };
            };
            """);

        Assert.Equal("basic", Resolve("pl").Section);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAnInclude_ComposesTheIncludedKeysBeneathTheIncludersOwn()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ q, Q ] };
                key <AD02> { [ w, W ] };
            };
            """);
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "us(basic)"
                key <AD02> { [ w, W, oe, OE ] };
            };
            """);

        var resolved = Resolve("pl");

        Assert.Equal(["q", "Q"], KeysymsOf(resolved, "<AD01>"));
        Assert.Equal(["w", "W", "oe", "OE"], KeysymsOf(resolved, "<AD02>"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAnAugmentInclude_LeavesDefinitionsTheIncluderAlreadyMade()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ q ] };
                key <AD02> { [ w ] };
            };
            """);
        AddSymbols("custom", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ apostrophe ] };
                augment "us(basic)"
            };
            """);

        var resolved = Resolve("custom");

        Assert.Equal(["apostrophe"], KeysymsOf(resolved, "<AD01>"));
        Assert.Equal(["w"], KeysymsOf(resolved, "<AD02>"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAKeyStatementWithNoKeysyms_LeavesTheExistingOutputsAlone()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ q, Q ] };
            };
            """);
        AddSymbols("custom", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "us(basic)"
                key <AD01> { type[Group1] = "ALPHABETIC" };
            };
            """);

        Assert.Equal(["q", "Q"], KeysymsOf(Resolve("custom"), "<AD01>"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAReplaceKeyWithNoKeysyms_DiscardsTheDefinitionEntirely()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ q, Q ] };
            };
            """);
        AddSymbols("custom", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "us(basic)"
                replace key <AD01> { type[Group1] = "ALPHABETIC" };
            };
            """);

        Assert.Empty(KeysymsOf(Resolve("custom"), "<AD01>"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForASectionIncludingAnotherSectionOfItsOwnFile_IsNotTreatedAsACycle()
    {
        // pl(lefty) includes pl(basic). A visited set keyed on the file alone would break this.
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ q ] };
            };

            partial alphanumeric_keys
            xkb_symbols "lefty" {
                include "pl(basic)"
                key <AD02> { [ w ] };
            };
            """);

        var resolved = Resolve("pl", "lefty");

        Assert.Equal(["q"], KeysymsOf(resolved, "<AD01>"));
        Assert.Equal(["w"], KeysymsOf(resolved, "<AD02>"));
        Assert.DoesNotContain(
            resolved.Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.CompositionTargetUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAGenuineCycle_StopsAndKeepsWhatItAlreadyRead()
    {
        AddSymbols("a", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "b(basic)"
                key <AD01> { [ q ] };
            };
            """);
        AddSymbols("b", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "a(basic)"
                key <AD02> { [ w ] };
            };
            """);

        var resolved = Resolve("a");

        Assert.Equal(["q"], KeysymsOf(resolved, "<AD01>"));
        Assert.Equal(["w"], KeysymsOf(resolved, "<AD02>"));
        Assert.Contains(
            resolved.Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.CompositionTargetUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCompositionNestsPastTheCap_ReportsItAsAnError()
    {
        // Each file includes the next, so the chain is longer than the cap without repeating.
        for (var index = 0; index <= XkbSymbolsResolver.MaximumDepth + 2; index++)
        {
            AddSymbols($"f{index}", $$"""
                default partial alphanumeric_keys
                xkb_symbols "basic" {
                    include "f{{index + 1}}(basic)"
                    key <AD{{index:00}}> { [ q ] };
                };
                """);
        }

        var resolved = Resolve("f0");

        Assert.Contains(
            resolved.Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.CompositionDepthExceeded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAnAlternateInclude_ApproximatesItAsOverrideAndSaysSoOnce()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q ] }; };
            """);
        AddSymbols("custom", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                alternate "us(basic)"
                alternate "us(basic)"
            };
            """);

        var resolved = Resolve("custom");

        Assert.Equal(["q"], KeysymsOf(resolved, "<AD01>"));
        Assert.Single(
            resolved.Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.MergeModeApproximated);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAnIncludeIntoASecondGroup_SkipsItRatherThanOverwritingTheFirst()
    {
        AddSymbols("ru", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ Cyrillic_shorti ] }; };
            """);
        AddSymbols("custom", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ q ] };
                include "ru(basic):2"
            };
            """);

        var resolved = Resolve("custom");

        Assert.Equal(["q"], KeysymsOf(resolved, "<AD01>"));
        Assert.Contains(
            resolved.Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.AlternateGroupsIgnored);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAnIncludeNoRootHolds_ReportsItAndKeepsTheRestOfTheLayout()
    {
        AddSymbols("custom", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "missing(basic)"
                key <AD01> { [ q ] };
            };
            """);

        var resolved = Resolve("custom");

        Assert.Equal(["q"], KeysymsOf(resolved, "<AD01>"));
        var diagnostic = Assert.Single(
            resolved.Diagnostics,
            candidate => candidate.Code == LayoutImportDiagnosticCodes.CompositionTargetUnavailable);
        Assert.Equal(ValidationSeverity.Warning, diagnostic.Severity);
        Assert.Contains("missing(basic)", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAnIncludeNamingASectionTheFileLacks_ReportsItRatherThanFallingBackToTheDefault()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q ] }; };
            """);
        AddSymbols("custom", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "us(nosuchsection)"
                key <AD02> { [ w ] };
            };
            """);

        var resolved = Resolve("custom");

        Assert.DoesNotContain(resolved.Keys, key => key.KeyName == "<AD01>");
        Assert.Contains(
            resolved.Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.CompositionTargetUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_AcrossAChain_RecordsEveryContributingSectionInVisitOrder()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q ] }; };
            """);
        AddSymbols("latin", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "us(basic)"
                key <AD02> { [ w ] };
            };
            """);
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "latin"
                key <AD03> { [ e ] };
            };
            """);

        Assert.Equal(
            ["pl(basic)", "latin(basic)", "us(basic)"],
            Resolve("pl").IncludeChain);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForAKeyDefinedTwice_NamesTheSectionWhoseDefinitionWon()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q ] }; };
            """);
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                include "us(basic)"
                key <AD01> { [ apostrophe ] };
            };
            """);

        Assert.Equal("pl(basic)", Assert.Single(Resolve("pl").Keys).Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_CarriesTheParserFindingsOfEveryFileItRead()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                key <AD01> { [ q ], [ Cyrillic_shorti ] };
            };
            """);
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { include "us(basic)" };
            """);

        Assert.Contains(
            Resolve("pl").Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.AlternateGroupsIgnored);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenNoRootHoldsTheFile_ReturnsNullSoTheCallerCanSayWhichLayoutIsMissing()
    {
        Assert.Null(ResolveOrNull("nonexistent"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenTheFileLacksTheRequestedSection_ReturnsNull()
    {
        AddSymbols("us", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q ] }; };
            """);

        Assert.Null(ResolveOrNull("us", "nosuchsection"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveLayout_ComposesTheLayoutOntoTheCommonBase()
    {
        // `rules/evdev` resolves every layout to `pc+<layout>`, so a layout that writes two dozen
        // keys still describes a whole keyboard. Resolving the file alone describes two dozen keys.
        AddCommonBase();
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                name[Group1] = "Polish";
                key <AD01> { [ q, Q ] };
            };
            """);

        var resolved = ResolveLayout("pl");

        Assert.Equal(["<ESC>", "<LSGT>", "<AD01>"], resolved.Keys.Select(key => key.KeyName));
        Assert.Equal(["q", "Q"], KeysymsOf(resolved, "<AD01>"));
        Assert.Equal("Polish", resolved.DisplayName);
        Assert.Equal("pl(basic)", resolved.Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveLayout_MarksTheKeysTheCommonBaseContributed()
    {
        // Every layout inherits the same base, so a key only the base defines says nothing about
        // this layout. Geometry inference and the fidelity report both need to tell the two apart.
        AddCommonBase();
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q, Q ] }; };
            """);

        var resolved = ResolveLayout("pl");

        Assert.True(Assert.Single(resolved.Keys, key => key.KeyName == "<ESC>").FromCommonBase);
        Assert.False(Assert.Single(resolved.Keys, key => key.KeyName == "<AD01>").FromCommonBase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveLayout_ForAKeyTheBaseAlsoDefines_LetsTheLayoutWin()
    {
        AddCommonBase();
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <LSGT> { [ backslash, bar ] }; };
            """);

        var resolved = ResolveLayout("pl");
        var key = Assert.Single(resolved.Keys, candidate => candidate.KeyName == "<LSGT>");

        Assert.Equal(["backslash", "bar"], key.Keysyms);
        Assert.Equal("pl(basic)", key.Origin);
        Assert.False(key.FromCommonBase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveLayout_NamesTheBaseFirstInTheIncludeChain()
    {
        AddCommonBase();
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q ] }; };
            """);

        Assert.Equal(["pc(pc105)", "pl(basic)"], ResolveLayout("pl").IncludeChain);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveLayout_WhenTheBaseNamesAGroup_KeepsTheLayoutsOwnName()
    {
        // A base that named a group would be naming every layout composed onto it.
        AddSymbols(XkbCommonBase.FileName, """
            default partial alphanumeric_keys
            xkb_symbols "pc105" {
                name[Group1] = "Generic";
                key <ESC> { [ Escape ] };
            };
            """);
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" {
                name[Group1] = "Polish";
                key <AD01> { [ q ] };
            };
            """);

        Assert.Equal("Polish", ResolveLayout("pl").DisplayName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveLayout_WhenNoRootHoldsTheBase_ResolvesTheLayoutAloneAndSaysNothing()
    {
        // The base is an inference made on the layout's behalf, not something the layout asked
        // for, so a root without one is not a finding against the import.
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q ] }; };
            """);

        var resolved = ResolveLayout("pl");

        Assert.Equal(["<AD01>"], resolved.Keys.Select(key => key.KeyName));
        Assert.Empty(resolved.Diagnostics);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ComposesNoBase_SoACallerCanStillAskAboutTheFileItself()
    {
        AddCommonBase();
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { key <AD01> { [ q ] }; };
            """);

        Assert.Equal(["<AD01>"], Resolve("pl").Keys.Select(key => key.KeyName));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ForASectionItNeverMerged_DoesNotCarryItsFindings()
    {
        // A symbols file is read whole and mostly unused: `keypad` holds overlay sections no
        // layout composes. Their losses are not this layout's.
        AddSymbols("keypad", """
            default hidden partial keypad_keys
            xkb_symbols "x11" { key <KP7> { [ KP_Home, KP_7 ] }; };

            hidden partial keypad_keys
            xkb_symbols "overlay1" {
                key <KP7> { [ KP_Home ], overlay1 = <KO7> };
            };
            """);
        AddSymbols("pl", """
            default partial alphanumeric_keys
            xkb_symbols "basic" { include "keypad(x11)" };
            """);

        Assert.DoesNotContain(
            Resolve("pl").Diagnostics,
            diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.UnsupportedConstructIgnored);
    }
}
