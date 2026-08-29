using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbSymbolsLexerTests
{
    private static IReadOnlyList<XkbSymbolsToken> Meaningful(string text) =>
        [.. XkbSymbolsLexer.Tokenize(text).Where(token => token.Kind != XkbSymbolsTokenKind.EndOfFile)];

    [Fact]
    [Trait("Category", "Unit")]
    public void Tokenize_ForAKeyStatement_SeparatesNamesSymbolsAndPunctuation()
    {
        var tokens = Meaningful("key <AD01> { [ q, Q ] };");

        Assert.Equal(
            [
                (XkbSymbolsTokenKind.Identifier, "key"),
                (XkbSymbolsTokenKind.KeyName, "<AD01>"),
                (XkbSymbolsTokenKind.LeftBrace, "{"),
                (XkbSymbolsTokenKind.LeftBracket, "["),
                (XkbSymbolsTokenKind.Identifier, "q"),
                (XkbSymbolsTokenKind.Comma, ","),
                (XkbSymbolsTokenKind.Identifier, "Q"),
                (XkbSymbolsTokenKind.RightBracket, "]"),
                (XkbSymbolsTokenKind.RightBrace, "}"),
                (XkbSymbolsTokenKind.Semicolon, ";")
            ],
            tokens.Select(token => (token.Kind, token.Text)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Tokenize_ForAQuotedString_StripsTheQuotes()
    {
        var token = Assert.Single(Meaningful("\"English (US)\""));

        Assert.Equal(XkbSymbolsTokenKind.QuotedString, token.Kind);
        Assert.Equal("English (US)", token.Text);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Tokenize_ForKeysymsThatLookLikeNumbers_KeepsThemAsIdentifiers()
    {
        // `1` and `0x01000105` are keysym names, not arithmetic; the decoder wants them as written.
        var tokens = Meaningful("[ 1, U0105, 0x01000105 ]");

        Assert.Equal(
            ["1", "U0105", "0x01000105"],
            tokens.Where(token => token.Kind == XkbSymbolsTokenKind.Identifier).Select(token => token.Text));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Tokenize_ForCommentedLines_DropsThemAndKeepsTheLineCount()
    {
        var tokens = Meaningful("// leading\n# also a comment\nkey // trailing\n");

        var token = Assert.Single(tokens);
        Assert.Equal("key", token.Text);
        Assert.Equal(3, token.Line);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Tokenize_ForKeyDotType_SplitsOnTheDotSoTheParserCanTellItFromAKeyStatement()
    {
        var tokens = Meaningful("key.type");

        Assert.Equal(
            [XkbSymbolsTokenKind.Identifier, XkbSymbolsTokenKind.Dot, XkbSymbolsTokenKind.Identifier],
            tokens.Select(token => token.Kind));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Tokenize_ForAnUnterminatedString_EndsItAtTheLineBreak()
    {
        // Swallowing the rest of the file would cost every statement after one stray quote.
        var tokens = Meaningful("name = \"unterminated\nkey <AD01>");

        Assert.Equal("unterminated", tokens.Single(token => token.Kind == XkbSymbolsTokenKind.QuotedString).Text);
        Assert.Contains(tokens, token => token is { Kind: XkbSymbolsTokenKind.KeyName, Text: "<AD01>" });
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Tokenize_ForACharacterItDoesNotKnow_EmitsItAsUnknownRatherThanFailing()
    {
        var tokens = Meaningful("key ! <AD01>");

        Assert.Contains(tokens, token => token is { Kind: XkbSymbolsTokenKind.Unknown, Text: "!" });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Tokenize_ForAnyInput_AlwaysEndsWithAnEndOfFileToken()
    {
        // The parser reads the current token without checking bounds, which this guarantees.
        Assert.Equal(XkbSymbolsTokenKind.EndOfFile, XkbSymbolsLexer.Tokenize(string.Empty)[^1].Kind);
        Assert.Equal(XkbSymbolsTokenKind.EndOfFile, XkbSymbolsLexer.Tokenize("key")[^1].Kind);
    }
}
