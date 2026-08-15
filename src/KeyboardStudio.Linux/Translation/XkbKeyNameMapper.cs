namespace KeyboardStudio.Linux;

public sealed class XkbKeyNameMapper : IXkbKeyNameMapper
{
    public const string UnsupportedPhysicalKeyCode = "KSL001";

    private static readonly Dictionary<string, string> CommonKeyNames = new(StringComparer.Ordinal)
    {
        ["Escape"] = "<ESC>",
        ["F1"] = "<FK01>", ["F2"] = "<FK02>", ["F3"] = "<FK03>", ["F4"] = "<FK04>",
        ["F5"] = "<FK05>", ["F6"] = "<FK06>", ["F7"] = "<FK07>", ["F8"] = "<FK08>",
        ["F9"] = "<FK09>", ["F10"] = "<FK10>", ["F11"] = "<FK11>", ["F12"] = "<FK12>",
        ["PrintScreen"] = "<PRSC>", ["ScrollLock"] = "<SCLK>", ["Pause"] = "<PAUS>",
        ["Backquote"] = "<TLDE>",
        ["Digit1"] = "<AE01>", ["Digit2"] = "<AE02>", ["Digit3"] = "<AE03>",
        ["Digit4"] = "<AE04>", ["Digit5"] = "<AE05>", ["Digit6"] = "<AE06>",
        ["Digit7"] = "<AE07>", ["Digit8"] = "<AE08>", ["Digit9"] = "<AE09>",
        ["Digit0"] = "<AE10>", ["Minus"] = "<AE11>", ["Equal"] = "<AE12>",
        ["Backspace"] = "<BKSP>",
        ["Insert"] = "<INS>", ["Home"] = "<HOME>", ["PageUp"] = "<PGUP>",
        ["Delete"] = "<DELE>", ["End"] = "<END>", ["PageDown"] = "<PGDN>",
        ["NumLock"] = "<NMLK>", ["NumpadDivide"] = "<KPDV>",
        ["NumpadMultiply"] = "<KPMU>", ["NumpadSubtract"] = "<KPSU>",
        ["Tab"] = "<TAB>",
        ["KeyQ"] = "<AD01>", ["KeyW"] = "<AD02>", ["KeyE"] = "<AD03>",
        ["KeyR"] = "<AD04>", ["KeyT"] = "<AD05>", ["KeyY"] = "<AD06>",
        ["KeyU"] = "<AD07>", ["KeyI"] = "<AD08>", ["KeyO"] = "<AD09>",
        ["KeyP"] = "<AD10>", ["BracketLeft"] = "<AD11>", ["BracketRight"] = "<AD12>",
        ["Enter"] = "<RTRN>",
        ["Numpad7"] = "<KP7>", ["Numpad8"] = "<KP8>", ["Numpad9"] = "<KP9>",
        ["NumpadAdd"] = "<KPAD>",
        ["CapsLock"] = "<CAPS>",
        ["KeyA"] = "<AC01>", ["KeyS"] = "<AC02>", ["KeyD"] = "<AC03>",
        ["KeyF"] = "<AC04>", ["KeyG"] = "<AC05>", ["KeyH"] = "<AC06>",
        ["KeyJ"] = "<AC07>", ["KeyK"] = "<AC08>", ["KeyL"] = "<AC09>",
        ["Semicolon"] = "<AC10>", ["Quote"] = "<AC11>",
        ["Numpad4"] = "<KP4>", ["Numpad5"] = "<KP5>", ["Numpad6"] = "<KP6>",
        ["ShiftLeft"] = "<LFSH>",
        ["KeyZ"] = "<AB01>", ["KeyX"] = "<AB02>", ["KeyC"] = "<AB03>",
        ["KeyV"] = "<AB04>", ["KeyB"] = "<AB05>", ["KeyN"] = "<AB06>",
        ["KeyM"] = "<AB07>", ["Comma"] = "<AB08>", ["Period"] = "<AB09>",
        ["Slash"] = "<AB10>", ["ShiftRight"] = "<RTSH>",
        ["ArrowUp"] = "<UP>", ["ArrowLeft"] = "<LEFT>",
        ["ArrowDown"] = "<DOWN>", ["ArrowRight"] = "<RGHT>",
        ["Numpad1"] = "<KP1>", ["Numpad2"] = "<KP2>", ["Numpad3"] = "<KP3>",
        ["NumpadEnter"] = "<KPEN>",
        ["ControlLeft"] = "<LCTL>", ["MetaLeft"] = "<LWIN>", ["AltLeft"] = "<LALT>",
        ["Space"] = "<SPCE>", ["AltRight"] = "<RALT>", ["MetaRight"] = "<RWIN>",
        ["ContextMenu"] = "<MENU>", ["ControlRight"] = "<RCTL>",
        ["Numpad0"] = "<KP0>", ["NumpadDecimal"] = "<KPDL>"
    };

    private static readonly Dictionary<string, string> Iso105KeyNames = CreateIso105KeyNames();
    private static readonly Dictionary<string, string> Ansi104KeyNames = CreateAnsi104KeyNames();

    public XkbKeyNameMappingResult Map(string templateId, string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var map = templateId switch
        {
            "iso-105" => Iso105KeyNames,
            "ansi-104" => Ansi104KeyNames,
            _ => null
        };
        if (map is not null && map.TryGetValue(keyId, out var keyName))
        {
            return new XkbKeyNameMappingResult(true, keyName, []);
        }

        return new XkbKeyNameMappingResult(
            false,
            null,
            [new XkbDiagnostic(
                UnsupportedPhysicalKeyCode,
                $"Physical key '{keyId}' in template '{templateId}' has no XKB key-name mapping.",
                keyId)]);
    }

    private static Dictionary<string, string> CreateIso105KeyNames()
    {
        var result = new Dictionary<string, string>(CommonKeyNames, StringComparer.Ordinal)
        {
            ["IntlHash"] = "<BKSL>",
            ["IntlBackslash"] = "<LSGT>"
        };
        return result;
    }

    private static Dictionary<string, string> CreateAnsi104KeyNames()
    {
        var result = new Dictionary<string, string>(CommonKeyNames, StringComparer.Ordinal)
        {
            ["Backslash"] = "<BKSL>"
        };
        return result;
    }
}
