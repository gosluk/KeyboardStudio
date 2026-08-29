namespace KeyboardStudio.Linux;

/// <summary>One lexical token of an <c>xkb_symbols</c> file.</summary>
/// <param name="Kind">The token's lexical category.</param>
/// <param name="Text">
/// The token's text: the identifier, the key name including its angle brackets, or a string's
/// contents without its quotes. Punctuation carries its own character.
/// </param>
/// <param name="Line">
/// One-based source line, carried so a diagnostic about a skipped statement can say where it was.
/// </param>
public readonly record struct XkbSymbolsToken(XkbSymbolsTokenKind Kind, string Text, int Line);
