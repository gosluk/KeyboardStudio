using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public static class WindowsModifierMapper
{
    public static WindowsModifierState Map(ModifierLayer layer) => layer switch
    {
        ModifierLayer.Default => new(WindowsModifierBits.None, WindowsModifierNumber.Default),
        ModifierLayer.Shift => new(WindowsModifierBits.Shift, WindowsModifierNumber.Shift),
        ModifierLayer.AltGr => new(
            WindowsModifierBits.Control | WindowsModifierBits.Alt,
            WindowsModifierNumber.AltGr),
        ModifierLayer.ShiftAltGr => new(
            WindowsModifierBits.Shift | WindowsModifierBits.Control | WindowsModifierBits.Alt,
            WindowsModifierNumber.ShiftAltGr),
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unsupported modifier layer.")
    };
}
