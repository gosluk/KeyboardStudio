namespace KeyboardStudio.Windows;

public sealed class WindowsModifierTable
{
    private WindowsModifierTable(IReadOnlyList<WindowsModifierState> states)
    {
        States = states;
    }

    public IReadOnlyList<WindowsModifierState> States { get; }

    public static WindowsModifierTable CreateV1() =>
        new(Array.AsReadOnly<WindowsModifierState>(
        [
            new(WindowsModifierBits.None, WindowsModifierNumber.Default),
            new(WindowsModifierBits.Shift, WindowsModifierNumber.Shift),
            new(WindowsModifierBits.Control, WindowsModifierNumber.Invalid),
            new(WindowsModifierBits.Shift | WindowsModifierBits.Control, WindowsModifierNumber.Invalid),
            new(WindowsModifierBits.Alt, WindowsModifierNumber.Invalid),
            new(WindowsModifierBits.Shift | WindowsModifierBits.Alt, WindowsModifierNumber.Invalid),
            new(WindowsModifierBits.Control | WindowsModifierBits.Alt, WindowsModifierNumber.AltGr),
            new(
                WindowsModifierBits.Shift | WindowsModifierBits.Control | WindowsModifierBits.Alt,
                WindowsModifierNumber.ShiftAltGr)
        ]));
}
