namespace KeyboardStudio.Windows;

public sealed record WindowsKeyboardLayout(
    IReadOnlyList<VscToVkMapping> VscToVkMappings,
    IReadOnlyList<ExtendedVscToVkMapping> ExtendedVscToVkMappings,
    WindowsModifierTable Modifiers,
    IReadOnlyList<WindowsMappingEntry> Entries);
