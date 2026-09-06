namespace KeyboardStudio.Linux;

/// <param name="AcceptedLoss">
/// What was written anyway, and what it cost. Empty unless the caller asked for keys to be written
/// incompletely; each entry names a key whose levels could not all be carried and says which. The
/// layout is usable — this is the record of what it is missing, for whoever has to agree to it.
/// </param>
public sealed record XkbUserVariantTranslationResult(
    bool Success,
    XkbUserVariantLayout? Layout,
    IReadOnlyList<XkbDiagnostic> Diagnostics)
{
    public IReadOnlyList<XkbDiagnostic> AcceptedLoss { get; init; } = [];
}
