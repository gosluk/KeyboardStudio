namespace KeyboardStudio.Linux;

/// <summary>
/// A statement the reader recognizes and deliberately does nothing with — <c>modifier_map</c>,
/// <c>virtual_modifiers</c>, <c>key.type</c>, a section-level <c>type</c>.
///
/// It is kept rather than dropped so that "understood and irrelevant" stays distinguishable from
/// "not understood": the latter raises <c>KSI022</c>, and conflating the two would either bury real
/// gaps in noise or hide them entirely. None of these statements can change which character a key
/// produces at a given level, which is why ignoring them is safe.
/// </summary>
/// <param name="Keyword">The statement's leading keyword, for tests and for explaining a parse.</param>
public sealed record XkbIgnoredStatement(string Keyword) : XkbSymbolsStatement;
