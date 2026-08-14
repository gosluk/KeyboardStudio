using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public sealed record WindowsMappingEntry(
    byte ScanCode,
    ModifierLayer Modifier,
    int UnicodeScalar);
