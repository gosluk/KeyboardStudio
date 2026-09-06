namespace KeyboardStudio.App;

/// <summary>
/// Which kind of output a layer row is editing.
/// </summary>
/// <remarks>
/// This is the editor's view of the <see cref="KeyboardStudio.Core.KeyOutput"/> hierarchy, flattened
/// into something a picker can bind to. It exists because the panel has to show an empty row as a
/// deliberate choice — "nothing", "a character I have not typed yet", "a key I have not picked yet"
/// are three different states, and a bare text box can only express the first.
/// </remarks>
public enum KeyOutputKind
{
    /// <summary>The layer produces nothing.</summary>
    None,

    /// <summary>The layer produces a single character.</summary>
    Character,

    /// <summary>The layer produces a functional key, such as an arrow or a numpad digit.</summary>
    SpecialKey
}
