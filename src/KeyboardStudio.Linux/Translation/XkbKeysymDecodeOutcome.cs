namespace KeyboardStudio.Linux;

/// <summary>
/// What the decoder made of a keysym.
///
/// Three of these produce no output and two of those share the diagnostic code <c>KSI032</c>, so
/// the outcome is the only thing that tells them apart. The distinction is worth keeping: a keysym
/// the model has no place for is a limit of this application, a keysym nothing recognises is a
/// fault in the file, and a fidelity report that ran them together would leave a user unable to
/// tell which of the two they were looking at.
/// </summary>
public enum XkbKeysymDecodeOutcome
{
    /// <summary>The keysym produces a character.</summary>
    Character,

    /// <summary>The keysym names a key, such as <c>F1</c> or <c>Return</c>.</summary>
    Key,

    /// <summary>The keysym is <c>NoSymbol</c> or <c>VoidSymbol</c>: an intentionally empty level.</summary>
    Empty,

    /// <summary>The keysym is a dead key, reported as <c>KSI031</c>.</summary>
    DeadKey,

    /// <summary>
    /// A real keysym with no counterpart in the model, such as <c>XF86AudioPlay</c>. Reported as
    /// <c>KSI032</c>.
    /// </summary>
    NotRepresentable,

    /// <summary>
    /// Text that names no keysym at all. Reported as <c>KSI032</c>. Rarer than it sounds and
    /// usually a mistake upstream — <c>symbols/th</c> writes <c>Voidsymbol</c> for
    /// <c>VoidSymbol</c>, which the user's own machine reads as nothing too.
    /// </summary>
    NotAKeysym
}
