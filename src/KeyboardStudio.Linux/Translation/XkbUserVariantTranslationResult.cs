namespace KeyboardStudio.Linux;

public sealed record XkbUserVariantTranslationResult(
    bool Success,
    XkbUserVariantLayout? Layout,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
