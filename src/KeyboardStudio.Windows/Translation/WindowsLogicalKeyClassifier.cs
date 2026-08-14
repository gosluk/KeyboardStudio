using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public static class WindowsLogicalKeyClassifier
{
    public static bool ProducesCharacters(LogicalKey logicalKey) => logicalKey switch
    {
        LogicalKey.A or
        LogicalKey.B or
        LogicalKey.C or
        LogicalKey.D or
        LogicalKey.E or
        LogicalKey.F or
        LogicalKey.G or
        LogicalKey.H or
        LogicalKey.I or
        LogicalKey.J or
        LogicalKey.K or
        LogicalKey.L or
        LogicalKey.M or
        LogicalKey.N or
        LogicalKey.O or
        LogicalKey.P or
        LogicalKey.Q or
        LogicalKey.R or
        LogicalKey.S or
        LogicalKey.T or
        LogicalKey.U or
        LogicalKey.V or
        LogicalKey.W or
        LogicalKey.X or
        LogicalKey.Y or
        LogicalKey.Z or
        LogicalKey.Digit0 or
        LogicalKey.Digit1 or
        LogicalKey.Digit2 or
        LogicalKey.Digit3 or
        LogicalKey.Digit4 or
        LogicalKey.Digit5 or
        LogicalKey.Digit6 or
        LogicalKey.Digit7 or
        LogicalKey.Digit8 or
        LogicalKey.Digit9 or
        LogicalKey.Backquote or
        LogicalKey.Minus or
        LogicalKey.Equal or
        LogicalKey.LeftBracket or
        LogicalKey.RightBracket or
        LogicalKey.Backslash or
        LogicalKey.InternationalBackslash or
        LogicalKey.InternationalHash or
        LogicalKey.Semicolon or
        LogicalKey.Quote or
        LogicalKey.Comma or
        LogicalKey.Period or
        LogicalKey.Slash or
        LogicalKey.Space or
        LogicalKey.NumpadDivide or
        LogicalKey.NumpadMultiply or
        LogicalKey.NumpadSubtract or
        LogicalKey.NumpadAdd or
        LogicalKey.NumpadDecimal or
        LogicalKey.Numpad0 or
        LogicalKey.Numpad1 or
        LogicalKey.Numpad2 or
        LogicalKey.Numpad3 or
        LogicalKey.Numpad4 or
        LogicalKey.Numpad5 or
        LogicalKey.Numpad6 or
        LogicalKey.Numpad7 or
        LogicalKey.Numpad8 or
        LogicalKey.Numpad9 => true,
        _ => false
    };
}
