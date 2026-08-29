namespace KeyboardStudio.Linux;

/// <summary>
/// Which set of phonetic key aliases a layout is read with.
///
/// <c>keycodes/aliases</c> defines <c>&lt;LatA&gt;</c> through <c>&lt;LatZ&gt;</c> three times over,
/// once per section, and <c>rules/evdev</c> chooses the section from the layout being loaded. The
/// same name therefore means different physical keys for different layouts: <c>&lt;LatZ&gt;</c> is
/// the bottom-row key on a US keyboard and the top-row one on a German keyboard. Import has to make
/// the same choice the host does, or a phonetic Russian layout written for a German keyboard comes
/// back with its Y and Z swapped.
/// </summary>
public enum XkbKeyAliasSet
{
    /// <summary>The default set, used for every layout not named in one of the others.</summary>
    Qwerty,

    /// <summary>Used for the Belgian and French layouts.</summary>
    Azerty,

    /// <summary>Used for the layouts of the German-speaking and central European countries.</summary>
    Qwertz
}
