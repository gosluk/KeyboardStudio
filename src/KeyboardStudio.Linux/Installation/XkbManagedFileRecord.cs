namespace KeyboardStudio.Linux;

public sealed record XkbManagedFileRecord(
    string RelativePath,
    string Sha256,
    bool WasCreatedByKeyboardStudio);
