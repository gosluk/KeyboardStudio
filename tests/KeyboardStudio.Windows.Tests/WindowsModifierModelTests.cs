using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsModifierModelTests
{
    public static TheoryData<ModifierLayer, WindowsModifierBits, WindowsModifierNumber> SupportedLayers => new()
    {
        { ModifierLayer.Default, WindowsModifierBits.None, WindowsModifierNumber.Default },
        { ModifierLayer.Shift, WindowsModifierBits.Shift, WindowsModifierNumber.Shift },
        {
            ModifierLayer.AltGr,
            WindowsModifierBits.Control | WindowsModifierBits.Alt,
            WindowsModifierNumber.AltGr
        },
        {
            ModifierLayer.ShiftAltGr,
            WindowsModifierBits.Shift | WindowsModifierBits.Control | WindowsModifierBits.Alt,
            WindowsModifierNumber.ShiftAltGr
        }
    };

    [Theory]
    [MemberData(nameof(SupportedLayers))]
    public void Map_WhenLayerIsSupported_ProducesWindowsBitsAndModifierNumber(
        ModifierLayer layer,
        WindowsModifierBits expectedBits,
        WindowsModifierNumber expectedNumber)
    {
        var state = WindowsModifierMapper.Map(layer);

        Assert.Equal(expectedBits, state.Bits);
        Assert.Equal(expectedNumber, state.Number);
    }

    [Fact]
    public void CreateV1_WhenEnumerated_DefinesAllBitCombinations()
    {
        var states = WindowsModifierTable.CreateV1().States;

        Assert.Equal(8, states.Count);
        Assert.Equal(Enumerable.Range(0, 8), states.Select(state => (int)state.Bits));
        Assert.Equal(WindowsModifierNumber.Invalid, states[2].Number);
        Assert.Equal(WindowsModifierNumber.Invalid, states[4].Number);
        Assert.Equal(WindowsModifierNumber.AltGr, states[6].Number);
        Assert.Equal(WindowsModifierNumber.ShiftAltGr, states[7].Number);
    }

    [Fact]
    public void Translate_WhenProjectIsValid_AttachesV1ModifierTable()
    {
        var layout = WindowsLayoutTranslator.Translate(DemoProjectFactory.Create());

        Assert.Equal(8, layout.Modifiers.States.Count);
    }
}
