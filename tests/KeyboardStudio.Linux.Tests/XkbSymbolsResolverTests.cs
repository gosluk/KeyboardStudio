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

    private ResolvedXkbSymbols? ResolveOrNull(string file, string? section = null)
    {
        var roots = new[] { new XkbDataRoot(Root, LayoutSourceOrigin.System) };
        var includeResolver = new XkbIncludeResolver(_fileSystem, roots);
        return new XkbSymbolsResolver(_fileSystem, includeResolver).Resolve(file, section);
    }

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
}
