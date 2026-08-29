using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// What one keysym became.
/// </summary>
/// <param name="Output">
/// The output to place on the layer. Never <see langword="null"/>: a keysym the model cannot
/// represent yields <see cref="NoOutput"/> and a diagnostic, so a caller can map every level
/// unconditionally and let the report explain the blanks.
/// </param>
/// <param name="Outcome">
/// Which of the six things happened. <see cref="Output"/> alone cannot say: three outcomes produce
/// <see cref="NoOutput"/> and two of those share a diagnostic code.
/// </param>
/// <param name="Diagnostic">
/// What was lost, or <see langword="null"/> when nothing was. One keysym can only produce one
/// finding, so this is a single value rather than the list the generation-side results carry.
/// </param>
public sealed record XkbKeysymDecodeResult(
    KeyOutput Output,
    XkbKeysymDecodeOutcome Outcome,
    LayoutImportDiagnostic? Diagnostic);
