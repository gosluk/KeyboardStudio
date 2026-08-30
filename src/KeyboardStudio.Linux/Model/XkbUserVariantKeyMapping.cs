namespace KeyboardStudio.Linux;

public sealed record XkbUserVariantKeyMapping(
    string PhysicalKeyId,
    string KeyName,
    XkbKeyType Type,
    IReadOnlyList<string> Keysyms);
