using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbLayoutTranslatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_FourLayers_MapsLevelsUnicodeAndAlphabeticType()
    {
        var project = CreateProject(
            "iso-105",
            "KeyA",
            LogicalKey.A,
            new Dictionary<ModifierLayer, KeyOutput>
            {
                [ModifierLayer.Default] = new CharacterOutput("a"),
                [ModifierLayer.Shift] = new CharacterOutput("A"),
                [ModifierLayer.AltGr] = new CharacterOutput("ą"),
                [ModifierLayer.ShiftAltGr] = new CharacterOutput("😀")
            });

        var result = new XkbLayoutTranslator().Translate(
            project,
            new XkbLayoutMetadata("test", "basic", "Test"));

        Assert.True(result.Success);
        var mapping = Assert.Single(result.Layout!.Mappings);
        Assert.Equal(XkbKeyType.FourLevelAlphabetic, mapping.Type);
        Assert.Equal(["a", "A", "U0105", "U1F600"], mapping.Keysyms);
        Assert.True(result.Layout.UsesLevelThree);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_MissingIntermediateLevel_UsesNoSymbol()
    {
        var project = CreateProject(
            "ansi-104",
            "Slash",
            LogicalKey.Slash,
            new Dictionary<ModifierLayer, KeyOutput>
            {
                [ModifierLayer.Default] = new CharacterOutput("/"),
                [ModifierLayer.AltGr] = new CharacterOutput("÷")
            });

        var result = new XkbLayoutTranslator().Translate(
            project,
            new XkbLayoutMetadata("test", "basic", "Test"));

        Assert.Equal(["slash", "NoSymbol", "U00F7"], Assert.Single(result.Layout!.Mappings).Keysyms);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(LogicalKey.Enter, "Return")]
    [InlineData(LogicalKey.ArrowLeft, "Left")]
    [InlineData(LogicalKey.NumpadAdd, "KP_Add")]
    public void Translate_LogicalOnlyMapping_UsesCanonicalKeysym(LogicalKey key, string expected)
    {
        var keyId = key switch
        {
            LogicalKey.ArrowLeft => "ArrowLeft",
            LogicalKey.NumpadAdd => "NumpadAdd",
            _ => "Enter"
        };
        var project = CreateProject("ansi-104", keyId, key, []);

        var result = new XkbLayoutTranslator().Translate(
            project,
            new XkbLayoutMetadata("test", "basic", "Test"));

        Assert.Equal(expected, Assert.Single(result.Layout!.Mappings).Keysyms[0]);
    }

    private static KeyboardProject CreateProject(
        string templateId,
        string keyId,
        LogicalKey logicalKey,
        Dictionary<ModifierLayer, KeyOutput> outputs) =>
        new()
        {
            Metadata = new ProjectMetadata
            {
                Name = "Test",
                Description = "Test",
                Version = "1.0.0",
                Language = "und"
            },
            Keyboard = new PhysicalKeyboard
            {
                Id = templateId,
                Keys = [new PhysicalKey { Id = keyId, ScanCode = 1 }]
            },
            Layout = new KeyboardLayout
            {
                Mappings =
                [
                    new KeyMapping
                    {
                        KeyId = keyId,
                        LogicalKey = logicalKey,
                        Outputs = outputs
                    }
                ]
            }
        };
}
