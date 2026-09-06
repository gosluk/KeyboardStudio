namespace KeyboardStudio.Linux;

/// <summary>
/// <c>key &lt;AD01&gt; { [ q, Q ] }</c> — the outputs of one physical key.
///
/// Only the first group's keysyms are carried. The domain model has one group, so keeping the rest
/// would mean inventing somewhere to put them; the parser drops them and raises <c>KSI020</c>
/// instead, which is the honest version of the same outcome.
/// </summary>
/// <param name="Merge">The prefix that decides which definition wins when both describe this key.</param>
/// <param name="KeyName">The XKB key name including its angle brackets, such as <c>&lt;AD01&gt;</c>.</param>
/// <param name="Keysyms">
/// The first group's keysym names in level order. Empty when the statement set only properties the
/// model does not hold, such as a key type — such a statement still exists, and a merge may need it.
/// </param>
/// <param name="KeyType">
/// The type the statement declared for the first group, or <see langword="null"/> when it declared
/// none. The model has no place for a type, but a derived layout that overrides this key must write
/// it back: the type is what decides which modifiers reach which level, and a key with a fifth
/// level has no way to reach it without the type that says so.
/// </param>
public sealed record XkbKeyStatement(
    XkbMergeMode Merge,
    string KeyName,
    IReadOnlyList<string> Keysyms,
    string? KeyType = null) : XkbSymbolsStatement;
