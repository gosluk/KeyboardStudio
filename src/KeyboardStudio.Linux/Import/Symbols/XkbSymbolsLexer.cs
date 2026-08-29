using System.Text;

namespace KeyboardStudio.Linux;

/// <summary>
/// Turns an <c>xkb_symbols</c> file into tokens.
///
/// The lexer never fails. Unterminated strings and key names end at the line break and malformed
/// characters become <see cref="XkbSymbolsTokenKind.Unknown"/> tokens, leaving every judgement about
/// well-formedness to the parser — which recovers by skipping a statement rather than by refusing
/// the file. A layout that is 95% readable is worth more than a refusal.
/// </summary>
public static class XkbSymbolsLexer
{
    /// <summary>Tokenizes <paramref name="text"/>, always ending with an end-of-file token.</summary>
    public static IReadOnlyList<XkbSymbolsToken> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = new List<XkbSymbolsToken>();
        var line = 1;
        var index = 0;

        while (index < text.Length)
        {
            var c = text[index];

            if (c == '\n')
            {
                line++;
                index++;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }

            // `//` is the XKB comment; `#` appears in hand-edited files and libxkbcommon accepts it.
            if (c == '#' || (c == '/' && index + 1 < text.Length && text[index + 1] == '/'))
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }

                continue;
            }

            if (c == '"')
            {
                tokens.Add(new XkbSymbolsToken(
                    XkbSymbolsTokenKind.QuotedString,
                    ReadDelimited(text, ref index, '"'),
                    line));
                continue;
            }

            if (c == '<')
            {
                var name = ReadDelimited(text, ref index, '>');
                tokens.Add(new XkbSymbolsToken(XkbSymbolsTokenKind.KeyName, $"<{name}>", line));
                continue;
            }

            if (IsIdentifierCharacter(c))
            {
                var start = index;
                while (index < text.Length && IsIdentifierCharacter(text[index]))
                {
                    index++;
                }

                tokens.Add(new XkbSymbolsToken(
                    XkbSymbolsTokenKind.Identifier,
                    text[start..index],
                    line));
                continue;
            }

            index++;
            tokens.Add(new XkbSymbolsToken(Punctuation(c), c.ToString(), line));
        }

        tokens.Add(new XkbSymbolsToken(XkbSymbolsTokenKind.EndOfFile, string.Empty, line));
        return tokens;
    }

    /// <summary>
    /// Reads from an opening delimiter to its closing one. A missing close ends the token at the
    /// line break rather than swallowing the rest of the file, which keeps one typo from costing
    /// every statement after it.
    /// </summary>
    private static string ReadDelimited(string text, ref int index, char close)
    {
        var content = new StringBuilder();
        index++;

        while (index < text.Length && text[index] != close && text[index] != '\n')
        {
            if (text[index] == '\\' && index + 1 < text.Length && text[index + 1] == close)
            {
                index++;
            }

            content.Append(text[index]);
            index++;
        }

        if (index < text.Length && text[index] == close)
        {
            index++;
        }

        return content.ToString();
    }

    private static bool IsIdentifierCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static XkbSymbolsTokenKind Punctuation(char c) => c switch
    {
        '{' => XkbSymbolsTokenKind.LeftBrace,
        '}' => XkbSymbolsTokenKind.RightBrace,
        '[' => XkbSymbolsTokenKind.LeftBracket,
        ']' => XkbSymbolsTokenKind.RightBracket,
        '(' => XkbSymbolsTokenKind.LeftParenthesis,
        ')' => XkbSymbolsTokenKind.RightParenthesis,
        '=' => XkbSymbolsTokenKind.Equals,
        ';' => XkbSymbolsTokenKind.Semicolon,
        ',' => XkbSymbolsTokenKind.Comma,
        '.' => XkbSymbolsTokenKind.Dot,
        _ => XkbSymbolsTokenKind.Unknown
    };
}
