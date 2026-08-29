namespace KeyboardStudio.Linux;

/// <summary>
/// One row of <see cref="XkbKeysymTable"/>: what a keysym mnemonic stands for.
/// </summary>
/// <param name="Value">
/// The keysym's numeric code. Not needed to decode a name, but it is what identifies a keysym in
/// the X11 protocol, so it is kept for the fidelity report and for tests that check the table
/// against its upstream sources.
/// </param>
/// <param name="Codepoint">
/// The Unicode scalar the keysym produces, or <see cref="XkbKeysymTable.NoCodepoint"/> when it
/// produces none — function keys, dead keys, and modifiers all name an action rather than a
/// character.
/// </param>
public readonly record struct XkbKeysymEntry(uint Value, int Codepoint);
