namespace KeyboardStudio.Linux;

/// <summary>
/// The component every layout is assembled on top of.
///
/// A symbols file is not a keyboard. <c>pl</c> writes the two dozen keys that make a layout Polish
/// and nothing else; Escape, the function row, the modifiers, the editing block, the arrows and the
/// keypad come from somewhere else entirely. That somewhere is <c>rules/evdev</c>, whose fallback
/// rule for every model and layout reads <c>pc+%l%(v)</c> — the layout is composed onto
/// <see cref="FileName"/>, and the result is what the user actually types on.
///
/// Import has to compose the same thing. Resolving the layout file alone yields a keyboard where
/// half the keys carry no output at all, which is not a partial import of the layout but a
/// misreading of how XKB layouts are written.
/// </summary>
public static class XkbCommonBase
{
    /// <summary>
    /// The symbols file the rules prepend. The rules name a different base for two models —
    /// <c>olpc</c> and Sun keyboards — and neither is a keyboard this application offers a template
    /// for, so the near-universal case is the only one composed.
    /// </summary>
    public const string FileName = "pc";
}
