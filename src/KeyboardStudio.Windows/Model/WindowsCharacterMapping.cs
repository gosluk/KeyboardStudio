namespace KeyboardStudio.Windows;

public sealed record WindowsCharacterMapping(
    WindowsVirtualKey VirtualKey,
    WindowsCharacterAttributes Attributes,
    char? Default,
    char? Shift,
    char? AltGr,
    char? ShiftAltGr);
