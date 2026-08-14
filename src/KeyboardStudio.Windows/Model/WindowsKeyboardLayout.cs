using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

internal sealed record WindowsMappingEntry(
    byte ScanCode,
    ModifierLayer Modifier,
    int UnicodeScalar);

internal sealed record WindowsKeyboardLayout(IReadOnlyList<WindowsMappingEntry> Entries);
