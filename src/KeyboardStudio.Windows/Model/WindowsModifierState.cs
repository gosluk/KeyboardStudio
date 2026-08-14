namespace KeyboardStudio.Windows;

public sealed record WindowsModifierState(
    WindowsModifierBits Bits,
    WindowsModifierNumber Number);
