using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Decodes XKB keysym names into model outputs, inverting <see cref="XkbKeysymMapper"/>.
///
/// The order the forms are tried in is the whole design. Function keys are recognised before the
/// character table, because keysyms such as <c>Return</c>, <c>Tab</c> and <c>KP_Multiply</c> carry a
/// Unicode annotation upstream and would otherwise import as a control character or a stray
/// asterisk instead of the key they name. Letters and digits are deliberately absent from the
/// function table for the mirror-image reason: <c>a</c> must stay the character <c>a</c>, or a
/// Dvorak import would label every key by its physical position and lose the layout entirely.
/// </summary>
public sealed class XkbKeysymDecoder : IXkbKeysymDecoder
{
    /// <summary>
    /// Keysyms that name a key rather than a character. Read in preference to the generated
    /// character table, and kept by hand rather than generated because it is the inverse of a
    /// hand-written mapping: every entry <see cref="XkbKeysymMapper"/> can produce appears here, so
    /// that generation and import round-trip, plus the inbound-only aliases below that a layout may
    /// use but generation never writes.
    /// </summary>
    private static readonly FrozenDictionary<string, LogicalKey> FunctionKeysyms = BuildFunctionKeysyms();

    /// <summary>
    /// The four ways a symbols file says "nothing here". libxkbcommon's keymap parser resolves
    /// these itself, before any keysym lookup and without regard to case, which is why they are
    /// matched case-insensitively while every other keysym name is not: xkeyboard-config really
    /// does write <c>noSymbol</c> in symbols/ge and <c>Voidsymbol</c> in symbols/th, and treating
    /// either as a mistake would report a loss on a key the user's own machine reads as empty.
    /// </summary>
    private static readonly FrozenSet<string> EmptyLevelKeywords =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "NoSymbol", "VoidSymbol", "any", "none");

    private static readonly SearchValues<char> HexDigits =
        SearchValues.Create("0123456789abcdefABCDEF");

    private const uint MaxCodepoint = 0x10FFFF;

    /// <inheritdoc />
    public XkbKeysymDecodeResult Decode(string keysym, string? keyId = null, ModifierLayer? layer = null)
    {
        ArgumentNullException.ThrowIfNull(keysym);

        var name = keysym.Trim();

        if (name.Length == 0 || EmptyLevelKeywords.Contains(name))
        {
            return new XkbKeysymDecodeResult(new NoOutput(), XkbKeysymDecodeOutcome.Empty, null);
        }

        if (TryDecodeLiteral(name, out var literal))
        {
            return Character(literal, name, keyId, layer);
        }

        // XKeysymDB spelt some media keys with a separating underscore that the headers do not
        // have, so files still write XF86_ClearGrab for the keysym XF86ClearGrab. libxkbcommon
        // accepts both; a decoder that did not would drop keys the user's machine reads perfectly
        // well. Only the lookup is rewritten — diagnostics still quote what the file wrote.
        var lookup = name.StartsWith("XF86_", StringComparison.Ordinal)
            ? string.Concat("XF86", name.AsSpan(5))
            : name;

        if (FunctionKeysyms.TryGetValue(lookup, out var logicalKey))
        {
            return new XkbKeysymDecodeResult(new SpecialKeyOutput(logicalKey), XkbKeysymDecodeOutcome.Key, null);
        }

        // Checked before the character table rather than after, because a dead key is a known loss
        // with its own code and its own explanation, not merely something the table lacks.
        if (name.StartsWith("dead_", StringComparison.Ordinal))
        {
            return new XkbKeysymDecodeResult(
                new NoOutput(),
                XkbKeysymDecodeOutcome.DeadKey,
                new LayoutImportDiagnostic(
                    ValidationSeverity.Warning,
                    LayoutImportDiagnosticCodes.DeadKeyDropped,
                    $"'{name}' is a dead key, which this model does not represent. The layer was "
                    + "left unmapped rather than given the accent as a character.",
                    keyId,
                    layer));
        }

        if (XkbKeysymTable.TryGetCodepoint(lookup, out var codepoint))
        {
            return Character(codepoint, name, keyId, layer);
        }

        // A keysym the table knows but the model cannot hold is a different situation from text
        // that is not a keysym at all — the first is a gap in the model, the second a gap in the
        // file — and a user reading the report can act on the distinction even though the code is
        // the same.
        var known = XkbKeysymTable.IsKnown(lookup);
        var message = known
            ? $"'{name}' has no equivalent in this model, so the layer was left unmapped."
            : $"'{name}' was not recognised as a keysym, so the layer was left unmapped.";

        return new XkbKeysymDecodeResult(
            new NoOutput(),
            known ? XkbKeysymDecodeOutcome.NotRepresentable : XkbKeysymDecodeOutcome.NotAKeysym,
            new LayoutImportDiagnostic(
                ValidationSeverity.Warning,
                LayoutImportDiagnosticCodes.OutputNotRepresentable,
                message,
                keyId,
                layer));
    }

    /// <summary>
    /// Reads the two forms that spell a character out rather than naming it: <c>U0105</c>, and the
    /// numeric keysym <c>0x01000105</c> along with the legacy values that stand for themselves.
    ///
    /// Both follow libxkbcommon's own parser — one to eight hex digits and nothing else after them
    /// — rather than a tidier rule of our own. Layouts really do write <c>U192</c> with three
    /// digits, and a decoder stricter than the host would refuse text the host accepts, while a
    /// looser one would accept text the host refuses. Either way import would stop describing what
    /// the user's machine actually does.
    /// </summary>
    private static bool TryDecodeLiteral(string name, out int codepoint)
    {
        codepoint = 0;

        if (name[0] == 'U' && TryParseHex(name.AsSpan(1), out var scalar) && scalar <= MaxCodepoint)
        {
            // Above U+00FF the keysym is the character; at or below it, libxkbcommon normalises to
            // the legacy keysym, which stands for that same character. Either way the character is
            // the value that was parsed.
            codepoint = (int)scalar;
            return true;
        }

        if (name.StartsWith("0x", StringComparison.Ordinal) && TryParseHex(name.AsSpan(2), out var value))
        {
            // Keysyms from 0x01000100 up are the character's own code plus 0x01000000, by the rule
            // keysymdef.h lays down for every keysym added since Unicode.
            if (value >= XkbKeysymTable.UnicodeOffset
                && value <= XkbKeysymTable.UnicodeOffset + MaxCodepoint)
            {
                codepoint = (int)(value - XkbKeysymTable.UnicodeOffset);
                return true;
            }

            // Below that, only printable ASCII and Latin-1 stand for themselves. Any other legacy
            // value would need a name for the table to translate it, and no symbols file in
            // xkeyboard-config writes one that way.
            if (value is >= 0x20 and <= 0x7E or >= 0xA0 and <= 0xFF)
            {
                codepoint = (int)value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parses one to eight hex digits that run to the end of the text, as libxkbcommon does.
    /// </summary>
    private static bool TryParseHex(ReadOnlySpan<char> digits, out uint value)
    {
        value = 0;
        return digits.Length is >= 1 and <= 8
            && !digits.ContainsAnyExcept(HexDigits)
            && uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static XkbKeysymDecodeResult Character(
        int codepoint,
        string name,
        string? keyId,
        ModifierLayer? layer)
    {
        // Control characters have no printable form, so a CharacterOutput holding one would show
        // the user an empty key and no reason for it. Any that names a key is in the function table
        // already; the rest are a genuine loss and are reported as one.
        if (Rune.IsValid(codepoint) && !Rune.IsControl(new Rune(codepoint)))
        {
            return new XkbKeysymDecodeResult(
                new CharacterOutput(new Rune(codepoint).ToString()),
                XkbKeysymDecodeOutcome.Character,
                null);
        }

        return new XkbKeysymDecodeResult(
            new NoOutput(),
            XkbKeysymDecodeOutcome.NotRepresentable,
            new LayoutImportDiagnostic(
                ValidationSeverity.Warning,
                LayoutImportDiagnosticCodes.OutputNotRepresentable,
                $"'{name}' produces a control character, which cannot be shown on a key, so the "
                + "layer was left unmapped.",
                keyId,
                layer));
    }

    private static FrozenDictionary<string, LogicalKey> BuildFunctionKeysyms()
    {
        var keysyms = new Dictionary<string, LogicalKey>(StringComparer.Ordinal)
        {
            ["Escape"] = LogicalKey.Escape,
            ["BackSpace"] = LogicalKey.Backspace,
            ["Tab"] = LogicalKey.Tab,
            ["Return"] = LogicalKey.Enter,
            ["Caps_Lock"] = LogicalKey.CapsLock,
            ["space"] = LogicalKey.Space,
            ["Print"] = LogicalKey.PrintScreen,
            ["Scroll_Lock"] = LogicalKey.ScrollLock,
            ["Pause"] = LogicalKey.Pause,
            ["Insert"] = LogicalKey.Insert,
            ["Delete"] = LogicalKey.Delete,
            ["Home"] = LogicalKey.Home,
            ["End"] = LogicalKey.End,
            ["Prior"] = LogicalKey.PageUp,
            ["Next"] = LogicalKey.PageDown,
            ["Up"] = LogicalKey.ArrowUp,
            ["Down"] = LogicalKey.ArrowDown,
            ["Left"] = LogicalKey.ArrowLeft,
            ["Right"] = LogicalKey.ArrowRight,
            ["Num_Lock"] = LogicalKey.NumLock,
            ["KP_Divide"] = LogicalKey.NumpadDivide,
            ["KP_Multiply"] = LogicalKey.NumpadMultiply,
            ["KP_Subtract"] = LogicalKey.NumpadSubtract,
            ["KP_Add"] = LogicalKey.NumpadAdd,
            ["KP_Enter"] = LogicalKey.NumpadEnter,
            ["KP_Decimal"] = LogicalKey.NumpadDecimal,
            ["Shift_L"] = LogicalKey.LeftShift,
            ["Shift_R"] = LogicalKey.RightShift,
            ["Control_L"] = LogicalKey.LeftControl,
            ["Control_R"] = LogicalKey.RightControl,
            ["Alt_L"] = LogicalKey.LeftAlt,
            ["ISO_Level3_Shift"] = LogicalKey.RightAlt,
            ["Super_L"] = LogicalKey.LeftMeta,
            ["Super_R"] = LogicalKey.RightMeta,
            ["Menu"] = LogicalKey.ContextMenu,

            // Inbound only. Generation never writes these, but layouts do, and each names a key the
            // model already has.
            ["Page_Up"] = LogicalKey.PageUp,
            ["Page_Down"] = LogicalKey.PageDown,
            ["Alt_R"] = LogicalKey.RightAlt,
            ["Mode_switch"] = LogicalKey.RightAlt,
            ["ISO_Left_Tab"] = LogicalKey.Tab,
            ["Sys_Req"] = LogicalKey.PrintScreen,
            ["Break"] = LogicalKey.Pause,

            // The numeric keypad's num-lock-off names. Layouts write these at level 1 and the digit
            // at level 2, so taking only the first level would otherwise lose the whole keypad.
            ["KP_Insert"] = LogicalKey.Numpad0,
            ["KP_End"] = LogicalKey.Numpad1,
            ["KP_Down"] = LogicalKey.Numpad2,
            ["KP_Next"] = LogicalKey.Numpad3,
            ["KP_Left"] = LogicalKey.Numpad4,
            ["KP_Begin"] = LogicalKey.Numpad5,
            ["KP_Right"] = LogicalKey.Numpad6,
            ["KP_Home"] = LogicalKey.Numpad7,
            ["KP_Up"] = LogicalKey.Numpad8,
            ["KP_Prior"] = LogicalKey.Numpad9,
            ["KP_Delete"] = LogicalKey.NumpadDecimal
        };

        for (var index = 1; index <= 24; index++)
        {
            keysyms[$"F{index}"] = LogicalKey.F1 + (index - 1);
        }

        for (var digit = 0; digit <= 9; digit++)
        {
            keysyms[$"KP_{digit}"] = LogicalKey.Numpad0 + digit;
        }

        return keysyms.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
