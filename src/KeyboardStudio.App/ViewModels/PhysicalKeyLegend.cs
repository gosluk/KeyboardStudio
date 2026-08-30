namespace KeyboardStudio.App;

/// <summary>
/// Maps physical key identifiers to the muted physical-name footer shown on every keycap.
/// </summary>
public static class PhysicalKeyLegend
{
    private const string LineBreak = "\n";

    private static readonly Dictionary<string, string> Legends =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Escape"] = "Esc",
            ["PrintScreen"] = "Print" + LineBreak + "Screen",
            ["ScrollLock"] = "Scroll" + LineBreak + "Lock",
            ["Pause"] = "Pause",

            ["Backquote"] = "`",
            ["Minus"] = "-",
            ["Equal"] = "=",
            ["Backspace"] = "Backspace",

            ["Tab"] = "Tab",
            ["BracketLeft"] = "[",
            ["BracketRight"] = "]",
            ["Backslash"] = "\\",
            ["Enter"] = "Enter",

            ["CapsLock"] = "Caps Lock",
            ["Semicolon"] = ";",
            ["Quote"] = "'",
            ["IntlHash"] = "#",

            ["ShiftLeft"] = "Shift",
            ["ShiftRight"] = "Shift",
            ["IntlBackslash"] = "\\",
            ["Comma"] = ",",
            ["Period"] = ".",
            ["Slash"] = "/",

            ["ControlLeft"] = "Ctrl",
            ["ControlRight"] = "Ctrl",
            ["MetaLeft"] = "Win",
            ["MetaRight"] = "Win",
            ["AltLeft"] = "Alt",
            ["AltRight"] = "Alt Gr",
            ["ContextMenu"] = "Menu",
            ["Space"] = "Space",

            ["Insert"] = "Insert",
            ["Delete"] = "Delete",
            ["Home"] = "Home",
            ["End"] = "End",
            ["PageUp"] = "Page" + LineBreak + "Up",
            ["PageDown"] = "Page" + LineBreak + "Down",

            ["ArrowUp"] = "↑",
            ["ArrowDown"] = "↓",
            ["ArrowLeft"] = "←",
            ["ArrowRight"] = "→",

            ["NumLock"] = "Num" + LineBreak + "Lock",
            ["NumpadDivide"] = "/",
            ["NumpadMultiply"] = "*",
            ["NumpadSubtract"] = "-",
            ["NumpadAdd"] = "+",
            ["NumpadEnter"] = "Enter",
            ["NumpadDecimal"] = ".",
        };

    /// <summary>
    /// Returns the keycap legend for <paramref name="keyId"/>, falling back to a readable form of
    /// the identifier for keys that are not explicitly mapped.
    /// </summary>
    public static string For(string keyId)
    {
        if (Legends.TryGetValue(keyId, out var legend))
        {
            return legend;
        }

        if (keyId.StartsWith("Numpad", StringComparison.Ordinal))
        {
            return keyId["Numpad".Length..];
        }

        if (keyId.StartsWith("Digit", StringComparison.Ordinal))
        {
            return keyId["Digit".Length..];
        }

        return keyId.Replace("Key", string.Empty, StringComparison.Ordinal);
    }
}
