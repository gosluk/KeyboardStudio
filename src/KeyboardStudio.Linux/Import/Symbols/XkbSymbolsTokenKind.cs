namespace KeyboardStudio.Linux;

/// <summary>
/// The lexical categories of an <c>xkb_symbols</c> file.
///
/// The set is deliberately coarse. The lexer's job is to hand the parser whole words, key names,
/// and strings rather than to decide what any of them mean; keywords stay
/// <see cref="Identifier"/>s because XKB has no reserved words — <c>type</c> is a statement in one
/// position and a keysym name in another.
/// </summary>
public enum XkbSymbolsTokenKind
{
    /// <summary>A run of letters, digits, and underscores: a keyword, a keysym, or a group name.</summary>
    Identifier,

    /// <summary>A key name in angle brackets, such as <c>&lt;AD01&gt;</c>, brackets included.</summary>
    KeyName,

    /// <summary>A double-quoted string, with the quotes stripped.</summary>
    QuotedString,

    LeftBrace,
    RightBrace,
    LeftBracket,
    RightBracket,
    LeftParenthesis,
    RightParenthesis,
    Equals,
    Semicolon,
    Comma,
    Dot,

    /// <summary>Any other character. The parser treats it as noise inside a statement it skips.</summary>
    Unknown,

    /// <summary>The end of input. Always the final token, so the parser never checks bounds.</summary>
    EndOfFile
}
