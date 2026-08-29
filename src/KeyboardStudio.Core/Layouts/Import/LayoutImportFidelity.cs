namespace KeyboardStudio.Core;

/// <summary>
/// How completely an import survived the trip into the domain model. Values ascend with loss, so
/// they compare and sort the same way <see cref="ValidationSeverity"/> does.
///
/// Import is deliberately lossy: a source may describe dead keys, extra modifier levels, or keys
/// with no counterpart on the chosen physical keyboard, none of which the model represents. Import
/// drops those and says so rather than refusing, because a starting point that is 95% right is
/// worth more than no starting point at all.
/// </summary>
public enum LayoutImportFidelity
{
    /// <summary>Every key and every output was represented. Nothing was dropped.</summary>
    Exact = 0,

    /// <summary>
    /// Every key was imported, but at least one output was dropped or approximated — a dead key,
    /// a level beyond the four the model has, or a symbol with no equivalent.
    /// </summary>
    Reduced = 1,

    /// <summary>
    /// At least one key was skipped outright, so the layout is incomplete rather than merely
    /// simplified.
    /// </summary>
    Partial = 2
}
