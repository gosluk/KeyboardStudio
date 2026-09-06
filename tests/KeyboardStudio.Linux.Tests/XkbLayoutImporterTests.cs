using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Projection of a flattened section onto a project. Built with the real resolver, decoder and
/// template provider: the point of these tests is what the three produce together, and substituting
/// any of them would test the substitute.
/// </summary>
public sealed class XkbLayoutImporterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForAnyLayout_PutsTheChosenTemplatesGeometryOnTheProject()
    {
        // Without this the project has mappings addressing keys its keyboard does not contain, and
        // the editor renders nothing at all.
        var result = Import(Symbols(Key("<AD01>", "q", "Q")));

        Assert.True(result.Success);
        Assert.Equal("iso-105", result.Project!.Keyboard.Id);
        Assert.Equal(105, result.Project.Keyboard.Keys.Count);
        Assert.Contains(result.Project.Keyboard.Keys, key => key.Id == "KeyQ");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForTheFirstFourLevels_MapsThemOntoTheFourLayersInOrder()
    {
        var result = Import(Symbols(Key("<AD01>", "q", "Q", "adiaeresis", "Adiaeresis")));

        var mapping = Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal(new CharacterOutput("q"), mapping.Outputs[ModifierLayer.Default]);
        Assert.Equal(new CharacterOutput("Q"), mapping.Outputs[ModifierLayer.Shift]);
        Assert.Equal(new CharacterOutput("ä"), mapping.Outputs[ModifierLayer.AltGr]);
        Assert.Equal(new CharacterOutput("Ä"), mapping.Outputs[ModifierLayer.ShiftAltGr]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForALevelBeyondTheFourth_DropsItAndSaysWhichKeyLostIt()
    {
        var result = Import(Symbols(Key("<AD01>", "q", "Q", "adiaeresis", "Adiaeresis", "ellipsis")));

        var mapping = Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal(4, mapping.Outputs.Count);

        var diagnostic = Assert.Single(
            result.Report.Diagnostics,
            item => item.Code == LayoutImportDiagnosticCodes.LayerBeyondModelDropped);
        Assert.Equal("KeyQ", diagnostic.KeyId);

        // Nothing was skipped outright, so the layout is reduced rather than partial.
        Assert.Equal(LayoutImportFidelity.Reduced, result.Report.Fidelity);
        Assert.Equal(0, result.Report.KeysSkipped);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForAKeyTheTemplateDoesNotHave_SkipsItAndCountsIt()
    {
        // <I120> is one of the keycodes evdev reserves for keys no PC keyboard exposes.
        var result = Import(Symbols(Key("<AD01>", "q"), Key("<I120>", "XF86AudioPlay")));

        Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal(1, result.Report.KeysImported);
        Assert.Equal(1, result.Report.KeysSkipped);
        Assert.Equal(LayoutImportFidelity.Partial, result.Report.Fidelity);
        Assert.Contains(
            result.Report.Diagnostics,
            item => item.Code == LayoutImportDiagnosticCodes.PhysicalKeyNotInTemplate);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_WhenTheChosenTemplateCannotBeLoaded_FailsWithoutAProject()
    {
        var result = Import(Symbols(Key("<AD01>", "q")), new LayoutImportOptions(TemplateId: "no-such-board"));

        Assert.False(result.Success);
        Assert.Null(result.Project);
        Assert.Contains(
            result.Report.Diagnostics,
            item => item.Code == LayoutImportDiagnosticCodes.TemplateNotAvailable
                && item.Severity == ValidationSeverity.Error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_WhenTheDefaultLayerNamesAKey_TakesTheLogicalKeyFromIt()
    {
        // A key that types nothing still has an identity, and only its own keysym carries it.
        var result = Import(Symbols(Key("<CAPS>", "Caps_Lock")));

        var mapping = Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal(LogicalKey.CapsLock, mapping.LogicalKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_WhenTheDefaultLayerTypesALetter_TakesTheLogicalKeyFromTheLetter()
    {
        var result = Import(Symbols(Key("<AD01>", "q", "Q")));

        Assert.Equal(LogicalKey.Q, Assert.Single(result.Project!.Layout.Mappings).LogicalKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForARearrangedLayout_KeepsThePhysicalKeysConventionalIdentity()
    {
        // Dvorak puts the apostrophe where QWERTY puts Q. The apostrophe names no logical key, so
        // without the conventional fallback every punctuation key in the layout would import as
        // LogicalKey.None and the mapping would forget which key was pressed.
        var result = Import(Symbols(
            Key("<AD01>", "apostrophe", "quotedbl"),
            Key("<AB08>", "w", "W")));

        var quote = Assert.Single(result.Project!.Layout.Mappings, mapping => mapping.KeyId == "KeyQ");
        Assert.Equal(LogicalKey.Q, quote.LogicalKey);
        Assert.Equal(new CharacterOutput("'"), quote.Outputs[ModifierLayer.Default]);

        // The key that does type a letter still takes its identity from the letter.
        var w = Assert.Single(result.Project.Layout.Mappings, mapping => mapping.KeyId == "Comma");
        Assert.Equal(LogicalKey.W, w.LogicalKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForEveryKeyOfBothTemplates_AssignsAConventionalLogicalKey()
    {
        // The conventional table has to cover both boards completely. It last did not, and the
        // twelve punctuation keys it omitted imported as LogicalKey.None on every layout.
        var mapper = new XkbKeyNameMapper();
        var provider = new KeyboardTemplateProvider();

        foreach (var templateId in new[] { "iso-105", "ansi-104" })
        {
            var keyboard = provider.Load(templateId);
            var names = keyboard.Keys
                .Select(key => (key.Id, mapper.Map(templateId, key.Id).KeyName))
                .Where(entry => entry.KeyName is not null)
                .ToArray();

            // periodcentered is a character that names no logical key of its own, so nothing but
            // the conventional table can give these mappings an identity.
            var symbols = Symbols(names.Select(entry => Key(entry.KeyName!, "periodcentered")).ToArray());
            var result = Import(symbols, new LayoutImportOptions(TemplateId: templateId));

            Assert.Equal(names.Length, result.Project!.Layout.Mappings.Count);

            var unidentified = result.Project.Layout.Mappings
                .Where(mapping => mapping.LogicalKey == LogicalKey.None)
                .Select(mapping => mapping.KeyId)
                .ToArray();

            Assert.Empty(unidentified);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForAKeyTheFileLeavesBlank_CountsItAsNeitherImportedNorSkipped()
    {
        // NoSymbol is a file saying "nothing here", which is not a loss and must not drag the
        // fidelity down to Partial.
        var result = Import(Symbols(Key("<AD01>", "q"), Key("<AD02>", "NoSymbol", "NoSymbol")));

        Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal(1, result.Report.KeysImported);
        Assert.Equal(0, result.Report.KeysSkipped);
        Assert.Equal(LayoutImportFidelity.Exact, result.Report.Fidelity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForAnUnrepresentableOutput_LeavesTheLayerUnmappedRatherThanBlank()
    {
        var result = Import(Symbols(Key("<AD01>", "q", "XF86AudioPlay")));

        var mapping = Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal([ModifierLayer.Default], mapping.Outputs.Keys);
        Assert.Contains(
            result.Report.Diagnostics,
            item => item.Code == LayoutImportDiagnosticCodes.OutputNotRepresentable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForAnyLayout_CarriesTheResolvedIncludeChainIntoTheReport()
    {
        var result = Import(Symbols(Key("<AD01>", "q")));

        Assert.Equal(["test(basic)", "latin(basic)"], result.Report.ResolvedIncludeChain);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LogicalKey_HasContiguousLetterAndDigitRuns()
    {
        // The importer maps a typed character to a logical key by arithmetic on the enum. If either
        // run stops being contiguous the arithmetic silently returns the wrong key.
        for (var offset = 0; offset < 26; offset++)
        {
            Assert.Equal(
                Enum.Parse<LogicalKey>(((char)('A' + offset)).ToString()),
                LogicalKey.A + offset);
        }

        for (var digit = 0; digit < 10; digit++)
        {
            Assert.Equal(
                Enum.Parse<LogicalKey>($"Digit{digit}"),
                LogicalKey.Digit0 + digit);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForTwoNamesOfOneKey_KeepsTheLaterStatementAndOnlyOneMapping()
    {
        // <LatQ> and <AD01> are one key: keycodes/evdev declares the first an alias of the second,
        // and a phonetic layout such as am(phonetic) writes both. The host reads the later
        // statement as the one that wins, and a project holding two mappings for one physical key
        // is one the editor refuses to validate.
        var result = Import(Symbols(
            Key("<AD01>", "Armenian_tche", "Armenian_TCHE"),
            Key("<LatQ>", "Armenian_ke", "Armenian_KE")));

        var mapping = Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal("KeyQ", mapping.KeyId);
        Assert.Equal(new CharacterOutput("ք"), mapping.Outputs[ModifierLayer.Default]);
        Assert.Equal(1, result.Report.KeysImported);
        Assert.Equal(0, result.Report.KeysSkipped);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForAKeyOnlyTheCommonBaseDefines_ImportsItWithoutGradingTheLayoutOnIt()
    {
        // The base is the same for every layout, so what it costs is not this layout's cost. The
        // function row's fifth level is a console switch no import can hold, and reporting it once
        // per layout would bury the findings that describe the layout.
        var result = Import(Symbols(BaseKey("<FK01>", "F1", "F1", "F1", "F1", "XF86_Switch_VT_1")));

        var mapping = Assert.Single(result.Project!.Layout.Mappings);
        Assert.Equal("F1", mapping.KeyId);
        Assert.Equal(LogicalKey.F1, mapping.LogicalKey);
        Assert.Empty(result.Report.Diagnostics);
        Assert.Equal(LayoutImportFidelity.Exact, result.Report.Fidelity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForAKeyTheLayoutDefines_StillReportsWhatItLost()
    {
        var result = Import(Symbols(Key("<AE11>", "minus", "underscore", "endash", "emdash", "U2212")));

        Assert.Contains(
            result.Report.Diagnostics,
            item => item.Code == LayoutImportDiagnosticCodes.LayerBeyondModelDropped);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Import_ForABaseKeyNoTemplateHas_LeavesItOutWithoutCallingTheImportPartial()
    {
        // The base describes every key a PC keyboard might have, Japanese and Korean keys included.
        // No template here has them, and the layout never asked for them.
        var result = Import(Symbols(
            BaseKey("<HKTG>", "Hiragana_Katakana"),
            Key("<AD01>", "q")));

        Assert.Equal("KeyQ", Assert.Single(result.Project!.Layout.Mappings).KeyId);
        Assert.Equal(0, result.Report.KeysSkipped);
        Assert.Empty(result.Report.Diagnostics);
    }

    private static LayoutImportResult Import(ResolvedXkbSymbols symbols, LayoutImportOptions? options = null)
    {
        var importer = new XkbLayoutImporter(
            new XkbKeyNameResolver(),
            new XkbKeysymDecoder(),
            new KeyboardTemplateProvider());

        return importer.Import(symbols, options ?? LayoutImportOptions.Default, registryEntry: null);
    }

    private static ResolvedXkbSymbols Symbols(params ResolvedXkbKey[] keys) =>
        new("/xkb/symbols/test", "basic", "Test Layout", keys, ["test(basic)", "latin(basic)"], []);

    private static ResolvedXkbKey Key(string name, params string[] keysyms) =>
        new(name, keysyms, "test(basic)");

    private static ResolvedXkbKey BaseKey(string name, params string[] keysyms) =>
        new(name, keysyms, "pc(pc105)", FromCommonBase: true);
}
