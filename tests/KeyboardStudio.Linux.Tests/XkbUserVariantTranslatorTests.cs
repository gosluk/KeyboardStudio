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
