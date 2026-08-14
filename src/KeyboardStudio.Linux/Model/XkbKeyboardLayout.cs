namespace KeyboardStudio.Linux;

public sealed record XkbKeyboardLayout(
    XkbLayoutMetadata Metadata,
    IReadOnlyList<XkbKeyMapping> Mappings,
    bool UsesLevelThree);
