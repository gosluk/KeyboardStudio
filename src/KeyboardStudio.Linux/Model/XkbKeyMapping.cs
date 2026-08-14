namespace KeyboardStudio.Linux;

public sealed record XkbKeyMapping(
    string PhysicalKeyId,
    string KeyName,
    XkbKeyType Type,
    IReadOnlyList<string> Keysyms);
