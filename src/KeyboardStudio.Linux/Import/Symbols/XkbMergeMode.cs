namespace KeyboardStudio.Linux;

/// <summary>
/// How a definition combines with one it was composed on top of. The prefix appears on
/// <c>include</c> and <c>key</c> statements alike, so the parser records it even though the
/// resolver is what acts on it.
/// </summary>
public enum XkbMergeMode
{
    /// <summary>No prefix. Behaves as <see cref="Override"/>; kept distinct so a file can be echoed back as written.</summary>
    Default = 0,

    /// <summary>The later definition wins.</summary>
    Override = 1,

    /// <summary>The existing definition wins; only previously undefined keys are added.</summary>
    Augment = 2,

    /// <summary>The existing definition is discarded and rebuilt.</summary>
    Replace = 3,

    /// <summary>An alternative definition. Approximated as <see cref="Override"/> with a <c>KSI023</c> finding.</summary>
    Alternate = 4
}
