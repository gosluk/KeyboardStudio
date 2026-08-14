namespace KeyboardStudio.Linux;

public sealed record XkbTranslationResult(
    bool Success,
    XkbKeyboardLayout? Layout,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
