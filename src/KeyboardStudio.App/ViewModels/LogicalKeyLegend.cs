using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// Formats logical keys for the compact assignment legend shown on a keycap.
/// </summary>
public static class LogicalKeyLegend
{
    private const string LineBreak = "\n";

    public static string For(LogicalKey key) =>
        key switch
        {
            LogicalKey.None => string.Empty,

            LogicalKey.Digit0 => "0",
            LogicalKey.Digit1 => "1",
            LogicalKey.Digit2 => "2",
            LogicalKey.Digit3 => "3",
            LogicalKey.Digit4 => "4",
            LogicalKey.Digit5 => "5",
            LogicalKey.Digit6 => "6",
            LogicalKey.Digit7 => "7",
            LogicalKey.Digit8 => "8",
            LogicalKey.Digit9 => "9",
            LogicalKey.Backquote => "`",
            LogicalKey.Minus => "-",
            LogicalKey.Equal => "=",
            LogicalKey.LeftBracket => "[",
            LogicalKey.RightBracket => "]",
            LogicalKey.Backslash or LogicalKey.InternationalBackslash => "\\",
            LogicalKey.InternationalHash => "#",
            LogicalKey.Semicolon => ";",
            LogicalKey.Quote => "'",
            LogicalKey.Comma => ",",
            LogicalKey.Period => ".",
            LogicalKey.Slash => "/",

            LogicalKey.Escape => "Esc",
            LogicalKey.CapsLock => "Caps" + LineBreak + "Lock",
            LogicalKey.PrintScreen => "Print" + LineBreak + "Screen",
            LogicalKey.ScrollLock => "Scroll" + LineBreak + "Lock",
            LogicalKey.PageUp => "Page" + LineBreak + "Up",
            LogicalKey.PageDown => "Page" + LineBreak + "Down",
            LogicalKey.ArrowUp => "↑",
            LogicalKey.ArrowDown => "↓",
            LogicalKey.ArrowLeft => "←",
            LogicalKey.ArrowRight => "→",

            LogicalKey.NumLock => "Num" + LineBreak + "Lock",
            LogicalKey.NumpadDivide => "Num /",
            LogicalKey.NumpadMultiply => "Num *",
            LogicalKey.NumpadSubtract => "Num -",
            LogicalKey.NumpadAdd => "Num +",
            LogicalKey.NumpadEnter => "Num" + LineBreak + "Enter",
            LogicalKey.NumpadDecimal => "Num .",
            LogicalKey.Numpad0 => "Num 0",
            LogicalKey.Numpad1 => "Num 1",
            LogicalKey.Numpad2 => "Num 2",
            LogicalKey.Numpad3 => "Num 3",
            LogicalKey.Numpad4 => "Num 4",
            LogicalKey.Numpad5 => "Num 5",
            LogicalKey.Numpad6 => "Num 6",
            LogicalKey.Numpad7 => "Num 7",
            LogicalKey.Numpad8 => "Num 8",
            LogicalKey.Numpad9 => "Num 9",

            LogicalKey.LeftShift => "L Shift",
            LogicalKey.RightShift => "R Shift",
            LogicalKey.LeftControl => "L Ctrl",
            LogicalKey.RightControl => "R Ctrl",
            LogicalKey.LeftAlt => "L Alt",
            LogicalKey.RightAlt => "R Alt",
            LogicalKey.LeftMeta => "L Meta",
            LogicalKey.RightMeta => "R Meta",
            LogicalKey.ContextMenu => "Menu",

            _ => key.ToString()
        };
}
