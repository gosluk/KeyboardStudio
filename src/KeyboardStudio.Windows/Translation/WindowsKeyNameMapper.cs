using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

internal static class WindowsKeyNameMapper
{
    public static bool TryGetDisplayName(LogicalKey logicalKey, out string displayName)
    {
        displayName = logicalKey switch
        {
            LogicalKey.Escape => "Esc",
            LogicalKey.Backspace => "Backspace",
            LogicalKey.Tab => "Tab",
            LogicalKey.Enter => "Enter",
            LogicalKey.CapsLock => "Caps Lock",
            >= LogicalKey.F1 and <= LogicalKey.F24 => $"F{(int)logicalKey - (int)LogicalKey.F1 + 1}",
            LogicalKey.PrintScreen => "Print Screen",
            LogicalKey.ScrollLock => "Scroll Lock",
            LogicalKey.Pause => "Pause",
            LogicalKey.Insert => "Insert",
            LogicalKey.Delete => "Delete",
            LogicalKey.Home => "Home",
            LogicalKey.End => "End",
            LogicalKey.PageUp => "Page Up",
            LogicalKey.PageDown => "Page Down",
            LogicalKey.ArrowUp => "Up",
            LogicalKey.ArrowDown => "Down",
            LogicalKey.ArrowLeft => "Left",
            LogicalKey.ArrowRight => "Right",
            LogicalKey.NumLock => "Num Lock",
            LogicalKey.NumpadDivide => "Num /",
            LogicalKey.NumpadMultiply => "Num *",
            LogicalKey.NumpadSubtract => "Num -",
            LogicalKey.NumpadAdd => "Num +",
            LogicalKey.NumpadEnter => "Num Enter",
            LogicalKey.NumpadDecimal => "Num Del",
            >= LogicalKey.Numpad0 and <= LogicalKey.Numpad9 =>
                $"Num {(int)logicalKey - (int)LogicalKey.Numpad0}",
            LogicalKey.LeftShift => "Shift",
            LogicalKey.RightShift => "Right Shift",
            LogicalKey.LeftControl => "Ctrl",
            LogicalKey.RightControl => "Right Ctrl",
            LogicalKey.LeftAlt => "Alt",
            LogicalKey.RightAlt => "Right Alt",
            LogicalKey.LeftMeta => "Left Windows",
            LogicalKey.RightMeta => "Right Windows",
            LogicalKey.ContextMenu => "Application",
            _ => string.Empty
        };

        return displayName.Length != 0;
    }
}
