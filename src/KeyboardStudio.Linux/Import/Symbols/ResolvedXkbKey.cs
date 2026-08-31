namespace KeyboardStudio.Linux;

/// <summary>
/// One key after every definition of it has been merged.
/// </summary>
/// <param name="KeyName">The XKB key name including its angle brackets, such as <c>&lt;AD01&gt;</c>.</param>
/// <param name="Keysyms">The first group's keysym names in level order.</param>
/// <param name="Origin">
/// The <c>file(section)</c> whose definition won. Kept so a surprising output can be traced to the
/// file that produced it, which composition otherwise makes very hard to work out.
/// </param>
/// <param name="FromCommonBase">
/// Whether the winning definition came from <see cref="XkbCommonBase"/> rather than from the layout
/// itself. Every layout inherits the same base, so a key that only the base defines says nothing
/// about the layout — which is exactly what geometry inference needs to know before reading
/// anything into a key's presence.
/// </param>
public sealed record ResolvedXkbKey(
    string KeyName,
    IReadOnlyList<string> Keysyms,
    string Origin,
    bool FromCommonBase = false);
