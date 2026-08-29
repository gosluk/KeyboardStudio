using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Reads an <c>xkb_symbols</c> file into sections and statements.
///
/// The parser accepts the whole statement vocabulary but consumes only what the domain model can
/// hold. Everything else falls into one of two buckets, and the distinction is the point: a
/// construct that cannot change which character a key produces — a key type, a modifier map — is
/// recognized and dropped in silence, while one that can is dropped with a finding. A statement the
/// parser does not recognize at all is skipped to the next <c>;</c> with <c>KSI022</c> rather than
/// aborting the file, because the goal is a usable starting point and not a conformant compiler.
/// </summary>
public sealed class XkbSymbolsParser
{
    /// <summary>Section flags that classify which keys a section defines, and change nothing else.</summary>
    private static readonly HashSet<string> IgnoredSectionFlags = new(StringComparer.Ordinal)
    {
        "alphanumeric_keys",
        "keypad_keys",
        "function_keys",
        "modifier_keys",
        "alternate_group"
    };

    /// <summary>Section-level statements that are read and discarded without comment.</summary>
    private static readonly HashSet<string> IgnoredSectionStatements = new(StringComparer.Ordinal)
    {
        "modifier_map",
        "virtual_modifiers",
        "type"
    };

    /// <summary>
    /// Key properties that cannot affect an output, so dropping them costs nothing. Matched without
    /// regard to case: the corpus writes the same property as <c>virtualmods</c> and
    /// <c>virtualMods</c>, and XKB treats both as the same name.
    /// </summary>
    private static readonly HashSet<string> IgnoredKeyProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "virtualmods",

        // The abbreviation of the same property. XKB accepts both spellings and
        // xkeyboard-config writes this one in symbols/level5, so a parser that knows only
        // the long form reports a gap in its grammar on any host shipping that file.
        "vmods",
        "repeat",
        "locks",
        "groupswrap",
        "groupsclamp",
        "groupsredirect"
    };

    /// <summary>Key properties that do change behavior, and so are dropped with <c>KSI021</c>.</summary>
    private static readonly HashSet<string> UnsupportedKeyProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "actions",
        "redirect",
        "overlay",
        "overlay1",
        "overlay2"
    };

    private readonly List<LayoutImportDiagnostic> _diagnostics = [];
    private IReadOnlyList<XkbSymbolsToken> _tokens = [];
    private int _position;

    /// <summary>
    /// Parses <paramref name="text"/> as the contents of <paramref name="path"/>. Never throws for
    /// malformed input: what could not be read comes back as diagnostics on the result.
    /// </summary>
    public XkbSymbolsFile Parse(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);

        _tokens = XkbSymbolsLexer.Tokenize(text);
        _position = 0;
        _diagnostics.Clear();

        var sections = new List<XkbSymbolsSection>();
        var flags = new List<string>();

        while (Current.Kind != XkbSymbolsTokenKind.EndOfFile)
        {
            if (Current.Kind != XkbSymbolsTokenKind.Identifier)
            {
                Advance();
                continue;
            }

            var word = Current.Text;

            if (string.Equals(word, "xkb_symbols", StringComparison.Ordinal))
            {
                Advance();
                sections.Add(ParseSection(flags));
                flags.Clear();
                continue;
            }

            if (IsSectionFlag(word))
            {
                flags.Add(word);
                Advance();
                continue;
            }

            // Another block type in the same file — an `xkb_keycodes`, say. Nothing here imports it,
            // but saying so is better than pretending the file held only what was read.
            Report(
                ValidationSeverity.Info,
                LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped,
                $"Skipped '{word}' at line {Current.Line}: only xkb_symbols blocks are read.");
            SkipUnknownConstruct();
            flags.Clear();
        }

        return new XkbSymbolsFile(path, sections, [.. _diagnostics]);
    }

    private XkbSymbolsSection ParseSection(List<string> flags)
    {
        var name = Current.Kind == XkbSymbolsTokenKind.QuotedString ? Current.Text : string.Empty;
        if (Current.Kind == XkbSymbolsTokenKind.QuotedString)
        {
            Advance();
        }

        var statements = new List<XkbSymbolsStatement>();

        if (Current.Kind == XkbSymbolsTokenKind.LeftBrace)
        {
            Advance();
            while (Current.Kind is not (XkbSymbolsTokenKind.RightBrace or XkbSymbolsTokenKind.EndOfFile))
            {
                var statement = ParseStatement();
                if (statement is not null)
                {
                    statements.Add(statement);
                }
            }

            if (Current.Kind == XkbSymbolsTokenKind.RightBrace)
            {
                Advance();
            }
        }

        SkipIf(XkbSymbolsTokenKind.Semicolon);

        return new XkbSymbolsSection(
            name,
            flags.Contains("default", StringComparer.Ordinal),
            flags.Contains("partial", StringComparer.Ordinal),
            flags.Contains("hidden", StringComparer.Ordinal),
            statements);
    }

    private XkbSymbolsStatement? ParseStatement()
    {
        if (Current.Kind == XkbSymbolsTokenKind.Semicolon)
        {
            Advance();
            return null;
        }

        if (Current.Kind != XkbSymbolsTokenKind.Identifier)
        {
            Advance();
            return null;
        }

        var merge = MergeModeOf(Current.Text);
        if (merge != XkbMergeMode.Default)
        {
            Advance();

            // A merge keyword can stand in for `include` entirely: `augment "us(basic)"` is an
            // include statement whose keyword happens to be the merge rule. Requiring the word
            // `include` here would drop those silently, which is the worst of the options.
            if (Current.Kind == XkbSymbolsTokenKind.QuotedString)
            {
                return ParseIncludeSpecification(merge);
            }

            if (Current.Kind != XkbSymbolsTokenKind.Identifier)
            {
                return null;
            }
        }

        var keyword = Current.Text;

        // `key.type = "..."` sets a default for the section; `key <NAME> { ... }` defines one key.
        if (string.Equals(keyword, "key", StringComparison.Ordinal))
        {
            Advance();
            if (Current.Kind == XkbSymbolsTokenKind.Dot)
            {
                SkipToStatementEnd();
                return new XkbIgnoredStatement("key.type");
            }

            return ParseKey(merge);
        }

        if (string.Equals(keyword, "include", StringComparison.Ordinal))
        {
            Advance();
            if (Current.Kind != XkbSymbolsTokenKind.QuotedString)
            {
                SkipToStatementEnd();
                return null;
            }

            return ParseIncludeSpecification(merge);
        }

        if (string.Equals(keyword, "name", StringComparison.Ordinal))
        {
            return ParseName();
        }

        if (IgnoredSectionStatements.Contains(keyword))
        {
            SkipToStatementEnd();
            return new XkbIgnoredStatement(keyword);
        }

        Report(
            ValidationSeverity.Info,
            LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped,
            $"Skipped the '{keyword}' statement at line {Current.Line}.");
        SkipToStatementEnd();
        return null;
    }

    /// <summary>
    /// Reads the quoted specification of an include, with the current token positioned on it.
    /// </summary>
    private XkbIncludeStatement ParseIncludeSpecification(XkbMergeMode merge)
    {
        var specification = Current.Text;
        Advance();
        SkipIf(XkbSymbolsTokenKind.Semicolon);
        return new XkbIncludeStatement(merge, specification);
    }

    private XkbNameStatement? ParseName()
    {
        var line = Current.Line;
        Advance();

        var group = 1;
        if (Current.Kind == XkbSymbolsTokenKind.LeftBracket)
        {
            Advance();
            group = GroupIndexOf(Current.Kind == XkbSymbolsTokenKind.Identifier ? Current.Text : null);
            SkipTo(XkbSymbolsTokenKind.RightBracket);
            SkipIf(XkbSymbolsTokenKind.RightBracket);
        }

        SkipIf(XkbSymbolsTokenKind.Equals);

        if (Current.Kind != XkbSymbolsTokenKind.QuotedString)
        {
            Report(
                ValidationSeverity.Info,
                LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped,
                $"Skipped a malformed 'name' statement at line {line}.");
            SkipToStatementEnd();
            return null;
        }

        var value = Current.Text;
        Advance();
        SkipIf(XkbSymbolsTokenKind.Semicolon);
        return new XkbNameStatement(group, value);
    }

    private XkbKeyStatement ParseKey(XkbMergeMode merge)
    {
        var line = Current.Line;
        var keyName = Current.Kind == XkbSymbolsTokenKind.KeyName ? Current.Text : "<?>";
        SkipIf(XkbSymbolsTokenKind.KeyName);

        IReadOnlyList<string> keysyms = [];
        var sawExtraGroup = false;
        var positionalGroup = 0;

        if (Current.Kind == XkbSymbolsTokenKind.LeftBrace)
        {
            Advance();
            while (Current.Kind is not (XkbSymbolsTokenKind.RightBrace or XkbSymbolsTokenKind.EndOfFile))
            {
                if (Current.Kind == XkbSymbolsTokenKind.Comma)
                {
                    Advance();
                    continue;
                }

                // A bare `[ ... ]` list takes its group from its position among the other lists.
                if (Current.Kind == XkbSymbolsTokenKind.LeftBracket)
                {
                    positionalGroup++;
                    var symbols = ParseSymbolList();
                    if (positionalGroup == 1)
                    {
                        keysyms = symbols;
                    }
                    else
                    {
                        sawExtraGroup = true;
                    }

                    continue;
                }

                if (Current.Kind != XkbSymbolsTokenKind.Identifier)
                {
                    Advance();
                    continue;
                }

                var property = Current.Text;
                Advance();

                var group = 1;
                if (Current.Kind == XkbSymbolsTokenKind.LeftBracket)
                {
                    Advance();
                    group = GroupIndexOf(Current.Kind == XkbSymbolsTokenKind.Identifier ? Current.Text : null);
                    SkipTo(XkbSymbolsTokenKind.RightBracket);
                    SkipIf(XkbSymbolsTokenKind.RightBracket);
                }

                SkipIf(XkbSymbolsTokenKind.Equals);

                if (string.Equals(property, "symbols", StringComparison.OrdinalIgnoreCase))
                {
                    var symbols = ParseSymbolList();
                    if (group == 1)
                    {
                        keysyms = symbols;
                    }
                    else
                    {
                        sawExtraGroup = true;
                    }

                    continue;
                }

                if (UnsupportedKeyProperties.Contains(property))
                {
                    Report(
                        ValidationSeverity.Warning,
                        LayoutImportDiagnosticCodes.UnsupportedConstructIgnored,
                        $"Key {keyName} at line {line} used '{property}', which has no equivalent in the model; it was ignored.");
                }
                else if (!IgnoredKeyProperties.Contains(property))
                {
                    Report(
                        ValidationSeverity.Info,
                        LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped,
                        $"Skipped the unrecognized property '{property}' of key {keyName} at line {line}.");
                }

                SkipKeyPropertyValue();
            }

            SkipIf(XkbSymbolsTokenKind.RightBrace);
        }

        // Only a terminator sitting directly after the closing brace belongs to this statement.
        // Scanning ahead for one would swallow the next key whenever a file omits it.
        SkipIf(XkbSymbolsTokenKind.Semicolon);

        if (sawExtraGroup)
        {
            Report(
                ValidationSeverity.Warning,
                LayoutImportDiagnosticCodes.AlternateGroupsIgnored,
                $"Key {keyName} at line {line} defined more than one group; only the first was imported.");
        }

        return new XkbKeyStatement(merge, keyName, keysyms);
    }

    /// <summary>
    /// Reads <c>[ a, A, aogonek ]</c>. An entry spanning several tokens — a hexadecimal keysym split
    /// by the lexer, say — is rejoined, since the decoder wants the written form back.
    /// </summary>
    private List<string> ParseSymbolList()
    {
        var symbols = new List<string>();
        SkipIf(XkbSymbolsTokenKind.LeftBracket);

        var entry = string.Empty;
        while (Current.Kind is not (XkbSymbolsTokenKind.RightBracket or XkbSymbolsTokenKind.EndOfFile))
        {
            if (Current.Kind == XkbSymbolsTokenKind.Comma)
            {
                symbols.Add(entry);
                entry = string.Empty;
                Advance();
                continue;
            }

            entry += Current.Text;
            Advance();
        }

        if (entry.Length > 0 || symbols.Count > 0)
        {
            symbols.Add(entry);
        }

        SkipIf(XkbSymbolsTokenKind.RightBracket);
        return symbols;
    }

    /// <summary>Consumes a property's value, whatever shape it has, up to the next entry or brace.</summary>
    private void SkipKeyPropertyValue()
    {
        var depth = 0;
        while (Current.Kind != XkbSymbolsTokenKind.EndOfFile)
        {
            switch (Current.Kind)
            {
                case XkbSymbolsTokenKind.LeftBracket:
                case XkbSymbolsTokenKind.LeftParenthesis:
                    depth++;
                    break;
                case XkbSymbolsTokenKind.RightBracket:
                case XkbSymbolsTokenKind.RightParenthesis:
                    depth--;
                    break;
                case XkbSymbolsTokenKind.Comma when depth <= 0:
                    return;
                case XkbSymbolsTokenKind.RightBrace when depth <= 0:
                    return;
                default:
                    break;
            }

            Advance();
        }
    }

    /// <summary>
    /// Skips a construct the parser does not read, whether it is a statement or a whole block.
    /// </summary>
    private void SkipUnknownConstruct()
    {
        while (Current.Kind is not (XkbSymbolsTokenKind.EndOfFile
            or XkbSymbolsTokenKind.Semicolon
            or XkbSymbolsTokenKind.LeftBrace))
        {
            Advance();
        }

        if (Current.Kind != XkbSymbolsTokenKind.LeftBrace)
        {
            SkipIf(XkbSymbolsTokenKind.Semicolon);
            return;
        }

        var depth = 0;
        while (Current.Kind != XkbSymbolsTokenKind.EndOfFile)
        {
            if (Current.Kind == XkbSymbolsTokenKind.LeftBrace)
            {
                depth++;
            }
            else if (Current.Kind == XkbSymbolsTokenKind.RightBrace)
            {
                depth--;
                if (depth == 0)
                {
                    Advance();
                    break;
                }
            }

            Advance();
        }

        SkipIf(XkbSymbolsTokenKind.Semicolon);
    }

    /// <summary>
    /// Skips the rest of a statement. Braces are counted rather than treated as a stop, because
    /// <c>modifier_map Shift { Shift_L, Shift_R };</c> carries a block of its own — halting at its
    /// closing brace would end the enclosing section early and orphan every statement after it.
    /// The enclosing section's own closing brace, at depth zero, still stops the skip, so a
    /// statement missing its terminator cannot consume the section around it.
    /// </summary>
    private void SkipToStatementEnd()
    {
        var depth = 0;
        while (Current.Kind != XkbSymbolsTokenKind.EndOfFile)
        {
            switch (Current.Kind)
            {
                case XkbSymbolsTokenKind.LeftBrace:
                    depth++;
                    break;
                case XkbSymbolsTokenKind.RightBrace when depth == 0:
                    return;
                case XkbSymbolsTokenKind.RightBrace:
                    depth--;
                    break;
                case XkbSymbolsTokenKind.Semicolon when depth == 0:
                    Advance();
                    return;
                default:
                    break;
            }

            Advance();
        }
    }

    /// <summary>
    /// Advances to <paramref name="kind"/>, stopping at a closing brace so a malformed statement
    /// cannot consume the rest of its section along with itself.
    /// </summary>
    private void SkipTo(XkbSymbolsTokenKind kind)
    {
        while (Current.Kind != kind
            && Current.Kind != XkbSymbolsTokenKind.EndOfFile
            && Current.Kind != XkbSymbolsTokenKind.RightBrace)
        {
            Advance();
        }
    }

    private void SkipIf(XkbSymbolsTokenKind kind)
    {
        if (Current.Kind == kind)
        {
            Advance();
        }
    }

    private static bool IsSectionFlag(string word) =>
        string.Equals(word, "default", StringComparison.Ordinal)
        || string.Equals(word, "partial", StringComparison.Ordinal)
        || string.Equals(word, "hidden", StringComparison.Ordinal)
        || IgnoredSectionFlags.Contains(word);

    private static XkbMergeMode MergeModeOf(string word) => word switch
    {
        "override" => XkbMergeMode.Override,
        "augment" => XkbMergeMode.Augment,
        "replace" => XkbMergeMode.Replace,
        "alternate" => XkbMergeMode.Alternate,
        _ => XkbMergeMode.Default
    };

    /// <summary>
    /// Reads the index out of <c>Group1</c> or <c>group1</c>. An unreadable index is treated as the
    /// first group: assuming otherwise would silently discard a key the file plainly defines.
    /// </summary>
    private static int GroupIndexOf(string? text) =>
        text is not null
        && text.StartsWith("group", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(text.AsSpan(5), out var index)
            ? index
            : 1;

    private XkbSymbolsToken Current => _tokens[_position];

    private void Advance()
    {
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }
    }

    private void Report(ValidationSeverity severity, string code, string message) =>
        _diagnostics.Add(new LayoutImportDiagnostic(severity, code, message));
}
