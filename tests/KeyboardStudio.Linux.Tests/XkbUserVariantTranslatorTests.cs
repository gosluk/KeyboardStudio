using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbUserVariantTranslatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenProjectIsUnchanged_ReturnsAnEmptySuccessfulVariant()
    {
        var mapping = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("a")));

        var result = Translate([mapping], [KeyMappingSnapshot.From(mapping)]);

        Assert.True(result.Success);
        Assert.Empty(result.Layout!.Mappings);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenOneLevelChanges_EmitsTheCompleteCurrentSupportedMapping()
    {
        var baseline = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new CharacterOutput("a")),
            (ModifierLayer.Shift, new CharacterOutput("A")),
            (ModifierLayer.AltGr, new CharacterOutput("ą")),
            (ModifierLayer.ShiftAltGr, new CharacterOutput("Ą")));
        var current = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new CharacterOutput("x")),
            (ModifierLayer.Shift, new CharacterOutput("X")),
            (ModifierLayer.AltGr, new CharacterOutput("ą")),
            (ModifierLayer.ShiftAltGr, new CharacterOutput("Ą")));

        var mapping = Assert.Single(Translate(
            [current],
            [KeyMappingSnapshot.From(baseline)]).Layout!.Mappings);

        Assert.Equal(["x", "X", "U0105", "U0104"], mapping.Keysyms);
        Assert.Equal(XkbKeyType.FourLevelSemialphabetic, mapping.Type);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenAltGrIsCleared_EmitsAnExplicitEmptyThirdLevel()
    {
        var baseline = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new CharacterOutput("a")),
            (ModifierLayer.Shift, new CharacterOutput("A")),
            (ModifierLayer.AltGr, new CharacterOutput("ą")));
        var current = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new CharacterOutput("a")),
            (ModifierLayer.Shift, new CharacterOutput("A")));

        var mapping = Assert.Single(Translate(
            [current],
            [KeyMappingSnapshot.From(baseline)]).Layout!.Mappings);

        Assert.Equal(["a", "A", "NoSymbol"], mapping.Keysyms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenNoOutputIsExplicit_EmitsNoSymbolWithoutLogicalFallback()
    {
        var baseline = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new CharacterOutput("a")));
        var current = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new NoOutput()));

        var mapping = Assert.Single(Translate(
            [current],
            [KeyMappingSnapshot.From(baseline)]).Layout!.Mappings);

        Assert.Equal(["NoSymbol"], mapping.Keysyms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenLogicalOnlyMappingChanges_UsesTheNewLogicalKeysym()
    {
        var baseline = Mapping("KeyA", LogicalKey.A);
        var current = Mapping("KeyA", LogicalKey.B);

        var mapping = Assert.Single(Translate(
            [current],
            [KeyMappingSnapshot.From(baseline)]).Layout!.Mappings);

        Assert.Equal(["b"], mapping.Keysyms);
        Assert.Equal(XkbKeyType.OneLevel, mapping.Type);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenMappingIsRemoved_OverridesInheritedLevelsWithNoSymbol()
    {
        var baseline = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new CharacterOutput("a")),
            (ModifierLayer.Shift, new CharacterOutput("A")));

        var mapping = Assert.Single(Translate(
            [],
            [KeyMappingSnapshot.From(baseline)]).Layout!.Mappings);

        Assert.Equal(["NoSymbol", "NoSymbol"], mapping.Keysyms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_SelectsAlphabeticSemialphabeticAndMixedKeypadTypes()
    {
        var baseline = new[]
        {
            Snapshot(Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("a")))),
            Snapshot(Mapping("KeyB", LogicalKey.B, (ModifierLayer.Default, new CharacterOutput("b")))),
            Snapshot(Mapping("Numpad1", LogicalKey.Numpad1, (ModifierLayer.Default, new CharacterOutput("1"))))
        };
        var current = new[]
        {
            Mapping(
                "KeyA",
                LogicalKey.A,
                (ModifierLayer.Default, new CharacterOutput("x")),
                (ModifierLayer.Shift, new CharacterOutput("X"))),
            Mapping(
                "KeyB",
                LogicalKey.B,
                (ModifierLayer.Default, new CharacterOutput("b")),
                (ModifierLayer.Shift, new CharacterOutput("B")),
                (ModifierLayer.AltGr, new CharacterOutput("β"))),
            Mapping(
                "Numpad1",
                LogicalKey.Numpad1,
                (ModifierLayer.Default, new CharacterOutput("1")),
                (ModifierLayer.Shift, new SpecialKeyOutput(LogicalKey.End)),
                (ModifierLayer.AltGr, new CharacterOutput("¹")))
        };

        var mappings = Translate(current, baseline).Layout!.Mappings
            .ToDictionary(mapping => mapping.PhysicalKeyId, StringComparer.Ordinal);

        Assert.Equal(XkbKeyType.Alphabetic, mappings["KeyA"].Type);
        Assert.Equal(XkbKeyType.FourLevelSemialphabetic, mappings["KeyB"].Type);
        Assert.Equal(XkbKeyType.FourLevelMixedKeypad, mappings["Numpad1"].Type);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_OrdersByXkbKeyNameAndReportsAltGrUse()
    {
        var result = Translate(
            [
                Mapping("KeyZ", LogicalKey.Z, (ModifierLayer.AltGr, new CharacterOutput("ż"))),
                Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("ą")))
            ],
            []);

        Assert.Equal(["<AB01>", "<AC01>"], result.Layout!.Mappings.Select(mapping => mapping.KeyName));
        Assert.True(result.Layout.UsesLevelThree);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenChangedSourceKeyWasLossy_BlocksGenerationForThatKey()
    {
        var baseline = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("a")));
        var current = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("x")));

        var result = Translate(
            [current],
            [KeyMappingSnapshot.From(baseline, isSafeToOverride: false)]);

        Assert.False(result.Success);
        Assert.Null(result.Layout);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(XkbUserVariantTranslator.UnsafeSourceBehaviorCode, diagnostic.Code);
        Assert.Equal("KeyA", diagnostic.KeyId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenTheKeyHasLevelsBeyondTheModel_KeepsThemAndTheTypeThatReachesThem()
    {
        // The German ß key: five levels, the fifth reached through Lock, which is why the source
        // declares a type for it. Changing what the key types must not cost it that level.
        var baseline = Mapping(
            "Minus",
            LogicalKey.Minus,
            (ModifierLayer.Default, new CharacterOutput("ß")),
            (ModifierLayer.Shift, new CharacterOutput("?")),
            (ModifierLayer.AltGr, new CharacterOutput("\\")),
            (ModifierLayer.ShiftAltGr, new CharacterOutput("¿")));
        var current = Mapping(
            "Minus",
            LogicalKey.Minus,
            (ModifierLayer.Default, new CharacterOutput("-")),
            (ModifierLayer.Shift, new CharacterOutput("_")));

        var result = Translate(
            [current],
            [
                new KeyMappingSnapshot(
                    baseline.KeyId,
                    baseline.LogicalKey,
                    baseline.Outputs,
                    isSafeToOverride: true,
                    ["ssharp", "question", "backslash", "questiondown", "U1E9E"],
                    "FOUR_LEVEL_PLUS_LOCK")
            ]);

        var mapping = Assert.Single(result.Layout!.Mappings);
        Assert.Equal(["minus", "underscore", "NoSymbol", "NoSymbol", "U1E9E"], mapping.Keysyms);
        Assert.Equal("FOUR_LEVEL_PLUS_LOCK", mapping.SourceTypeName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenALevelWasNeverRepresented_WritesTheSourceBackUnchanged()
    {
        // The editor shows nothing on the third and fourth levels because a dead key has no
        // character. Nothing was changed there, so nothing there may change.
        var baseline = Mapping(
            "Equal",
            LogicalKey.Equal,
            (ModifierLayer.Default, new CharacterOutput("´")));
        var current = Mapping(
            "Equal",
            LogicalKey.Equal,
            (ModifierLayer.Default, new CharacterOutput("=")));

        var result = Translate(
            [current],
            [
                new KeyMappingSnapshot(
                    baseline.KeyId,
                    baseline.LogicalKey,
                    baseline.Outputs,
                    isSafeToOverride: true,
                    ["acute", "dead_grave", "dead_cedilla", "dead_ogonek"])
            ]);

        var mapping = Assert.Single(result.Layout!.Mappings);
        Assert.Equal(["equal", "dead_grave", "dead_cedilla", "dead_ogonek"], mapping.Keysyms);
        Assert.Null(mapping.SourceTypeName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenTheUserClearsALevelTheImportHeld_StillEmptiesIt()
    {
        // The source also describes this level, but the user saw it and removed it. Restoring it
        // from the source would undo the edit instead of preserving what was never edited.
        var baseline = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new CharacterOutput("a")),
            (ModifierLayer.Shift, new CharacterOutput("A")),
            (ModifierLayer.AltGr, new CharacterOutput("ą")));
        var current = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, new CharacterOutput("a")),
            (ModifierLayer.Shift, new CharacterOutput("A")));

        var result = Translate(
            [current],
            [
                new KeyMappingSnapshot(
                    baseline.KeyId,
                    baseline.LogicalKey,
                    baseline.Outputs,
                    isSafeToOverride: true,
                    ["a", "A", "aogonek"])
            ]);

        var mapping = Assert.Single(result.Layout!.Mappings);
        Assert.Equal(["a", "A", "NoSymbol"], mapping.Keysyms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Translate_WhenASourceLevelIsNotAKeysymName_RefusesTheKeyRatherThanWriteIt()
    {
        var baseline = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("a")));
        var current = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("x")));

        var result = Translate(
            [current],
            [
                new KeyMappingSnapshot(
                    baseline.KeyId,
                    baseline.LogicalKey,
                    baseline.Outputs,
                    isSafeToOverride: true,
                    ["a", "] };  key <AB01> { [ z"])
            ]);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(XkbUserVariantTranslator.UnwritableSourceLevelCode, diagnostic.Code);
        Assert.Equal("KeyA", diagnostic.KeyId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Translate_WhenLevelsRunPastTheModelWithNoTypeToReachThem_RefusesTheKey()
    {
        var baseline = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("a")));
        var current = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, new CharacterOutput("x")));

        var result = Translate(
            [current],
            [
                new KeyMappingSnapshot(
                    baseline.KeyId,
                    baseline.LogicalKey,
                    baseline.Outputs,
                    isSafeToOverride: true,
                    ["a", "A", "NoSymbol", "NoSymbol", "U1E9E"])
            ]);

        Assert.False(result.Success);
        Assert.Equal(
            XkbUserVariantTranslator.UnwritableSourceLevelCode,
            Assert.Single(result.Diagnostics).Code);
    }

    private static XkbUserVariantTranslationResult Translate(
        IReadOnlyList<KeyMapping> current,
        IReadOnlyList<KeyMappingSnapshot> baseline)
    {
        var project = new KeyboardProject
        {
            Metadata = new ProjectMetadata { Name = "Test" },
            Keyboard = new PhysicalKeyboard { Id = "iso-105" },
            Layout = new KeyboardLayout { Mappings = current.ToList() }
        };
        return new XkbUserVariantTranslator().Translate(project, baseline, Metadata());
    }

    private static XkbUserVariantMetadata Metadata() => new(
        "7c31d5f2a19e40a4b0ef64f01a295135",
        "pl",
        "qwertz",
        "qwertz",
        "keyboardstudio_programmer",
        "Polish - KeyboardStudio");

    private static KeyMappingSnapshot Snapshot(KeyMapping mapping) => KeyMappingSnapshot.From(mapping);

    private static KeyMapping Mapping(
        string keyId,
        LogicalKey logicalKey,
        params (ModifierLayer Layer, KeyOutput Output)[] outputs) =>
        new()
        {
            KeyId = keyId,
            LogicalKey = logicalKey,
            Outputs = outputs.ToDictionary(item => item.Layer, item => item.Output)
        };
}
