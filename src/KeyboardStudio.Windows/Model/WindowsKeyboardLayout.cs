namespace KeyboardStudio.Windows;

public sealed record WindowsKeyboardLayout(
    IReadOnlyList<VscToVkMapping> VscToVkMappings,
    IReadOnlyList<ExtendedVscToVkMapping> ExtendedVscToVkMappings,
    IReadOnlyList<WindowsKeyNameMapping> KeyNames,
    IReadOnlyList<WindowsKeyNameMapping> ExtendedKeyNames,
    WindowsModifierTable Modifiers,
    WindowsCharacterTable Characters);
