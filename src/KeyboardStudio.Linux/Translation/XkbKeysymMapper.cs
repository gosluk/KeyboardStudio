using System.Globalization;
using System.Text;
using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

public sealed class XkbKeysymMapper : IXkbKeysymMapper
{
    private static readonly Dictionary<int, string> CanonicalCharacters = new()
    {
        [' '] = "space", ['!'] = "exclam", ['\"'] = "quotedbl", ['#'] = "numbersign",
        ['$'] = "dollar", ['%'] = "percent", ['&'] = "ampersand", ['\''] = "apostrophe",
        ['('] = "parenleft", [')'] = "parenright", ['*'] = "asterisk", ['+'] = "plus",
        [','] = "comma", ['-'] = "minus", ['.'] = "period", ['/'] = "slash",
        [':'] = "colon", [';'] = "semicolon", ['<'] = "less", ['='] = "equal",
        ['>'] = "greater", ['?'] = "question", ['@'] = "at", ['['] = "bracketleft",
        ['\\'] = "backslash", [']'] = "bracketright", ['^'] = "asciicircum",
        ['_'] = "underscore", ['`'] = "grave", ['{'] = "braceleft", ['|'] = "bar",
        ['}'] = "braceright", ['~'] = "asciitilde"
    };

    public bool TryMap(KeyOutput output, out string keysym)
    {
        ArgumentNullException.ThrowIfNull(output);
        switch (output)
        {
            case CharacterOutput character:
                keysym = MapCharacter(character.Value);
                return true;
            case SpecialKeyOutput special:
                return TryMap(special.Key, out keysym);
            case NoOutput:
                keysym = "NoSymbol";
                return true;
            default:
                keysym = string.Empty;
                return false;
        }
    }

    public bool TryMap(LogicalKey logicalKey, out string keysym)
    {
        if (logicalKey is >= LogicalKey.A and <= LogicalKey.Z)
        {
            keysym = logicalKey.ToString().ToLowerInvariant();
            return true;
        }

        if (logicalKey is >= LogicalKey.Digit0 and <= LogicalKey.Digit9)
        {
            keysym = logicalKey.ToString()[^1].ToString();
            return true;
        }

        if (logicalKey is >= LogicalKey.F1 and <= LogicalKey.F24)
        {
            keysym = logicalKey.ToString();
            return true;
        }

        if (logicalKey is >= LogicalKey.Numpad0 and <= LogicalKey.Numpad9)
        {
            keysym = $"KP_{logicalKey.ToString()[^1]}";
            return true;
        }

        keysym = logicalKey switch
        {
            LogicalKey.Backquote => "grave",
            LogicalKey.Minus => "minus",
            LogicalKey.Equal => "equal",
            LogicalKey.LeftBracket => "bracketleft",
            LogicalKey.RightBracket => "bracketright",
            LogicalKey.Backslash or LogicalKey.InternationalBackslash or LogicalKey.InternationalHash => "backslash",
            LogicalKey.Semicolon => "semicolon",
            LogicalKey.Quote => "apostrophe",
            LogicalKey.Comma => "comma",
            LogicalKey.Period => "period",
            LogicalKey.Slash => "slash",
            LogicalKey.Escape => "Escape",
            LogicalKey.Backspace => "BackSpace",
            LogicalKey.Tab => "Tab",
            LogicalKey.Enter => "Return",

            // The numpad's own Return. Every real layout binds <KPEN> to KP_Enter, and
            // applications that tell the two apart — a terminal, a spreadsheet — would see the
            // wrong one if this collapsed into Return.
            LogicalKey.NumpadEnter => "KP_Enter",
            LogicalKey.CapsLock => "Caps_Lock",
            LogicalKey.Space => "space",
            LogicalKey.PrintScreen => "Print",
            LogicalKey.ScrollLock => "Scroll_Lock",
            LogicalKey.Pause => "Pause",
            LogicalKey.Insert => "Insert",
            LogicalKey.Delete => "Delete",
            LogicalKey.Home => "Home",
            LogicalKey.End => "End",
            LogicalKey.PageUp => "Prior",
            LogicalKey.PageDown => "Next",
            LogicalKey.ArrowUp => "Up",
            LogicalKey.ArrowDown => "Down",
            LogicalKey.ArrowLeft => "Left",
            LogicalKey.ArrowRight => "Right",
            LogicalKey.NumLock => "Num_Lock",
            LogicalKey.NumpadDivide => "KP_Divide",
            LogicalKey.NumpadMultiply => "KP_Multiply",
            LogicalKey.NumpadSubtract => "KP_Subtract",
            LogicalKey.NumpadAdd => "KP_Add",
            LogicalKey.NumpadDecimal => "KP_Decimal",
            LogicalKey.LeftShift => "Shift_L",
            LogicalKey.RightShift => "Shift_R",
            LogicalKey.LeftControl => "Control_L",
            LogicalKey.RightControl => "Control_R",
            LogicalKey.LeftAlt => "Alt_L",
            LogicalKey.RightAlt => "ISO_Level3_Shift",
            LogicalKey.LeftMeta => "Super_L",
            LogicalKey.RightMeta => "Super_R",
            LogicalKey.ContextMenu => "Menu",
            _ => string.Empty
        };
        return keysym.Length > 0;
    }

    private static string MapCharacter(string value)
    {
        var rune = value.EnumerateRunes().Single();
        if (rune.IsAscii && char.IsAsciiLetterOrDigit((char)rune.Value))
        {
            return value;
        }

        if (CanonicalCharacters.TryGetValue(rune.Value, out var canonical))
        {
            return canonical;
        }

        return $"U{rune.Value.ToString("X4", CultureInfo.InvariantCulture)}";
    }
}
