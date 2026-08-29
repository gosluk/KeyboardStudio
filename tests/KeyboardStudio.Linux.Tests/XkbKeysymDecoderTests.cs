using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbKeysymDecoderTests
{
    private readonly XkbKeysymDecoder decoder = new();

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("NoSymbol")]
    [InlineData("VoidSymbol")]
    [InlineData("any")]
    [InlineData("none")]
    [InlineData("noSymbol")]
    [InlineData("Voidsymbol")]
    [InlineData("NONE")]
    public void Decode_ForAnEmptyLevel_IsNoOutputAndNotALoss(string keysym)
    {
        // All four spellings, in any case: libxkbcommon's keymap parser resolves them itself before
        // it looks a keysym up, so symbols/ge writing noSymbol and symbols/th writing Voidsymbol
        // are both empty levels on a real machine rather than the mistakes they look like.
        var result = decoder.Decode(keysym);

        Assert.IsType<NoOutput>(result.Output);
        Assert.Equal(XkbKeysymDecodeOutcome.Empty, result.Outcome);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decode_ForAKeysymThatIsNotAnEmptyLevelKeyword_StaysCaseSensitive()
    {
        // Only those four keywords ignore case, because only they are resolved by the parser rather
        // than looked up. Keysym names are matched exactly: aogonek and Aogonek are different
        // letters, and RETURN is not a keysym at all.
        Assert.Equal(new CharacterOutput("ą"), decoder.Decode("aogonek").Output);
        Assert.Equal(new CharacterOutput("Ą"), decoder.Decode("Aogonek").Output);
        Assert.Equal(XkbKeysymDecodeOutcome.NotAKeysym, decoder.Decode("RETURN").Outcome);
        Assert.Equal(XkbKeysymDecodeOutcome.NotAKeysym, decoder.Decode("DEAD_ACUTE").Outcome);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("XF86_ClearGrab", "XF86ClearGrab")]
    [InlineData("XF86_AudioPlay", "XF86AudioPlay")]
    public void Decode_ForAMediaKeysymSpeltWithASeparatingUnderscore_ReadsItAsTheHostDoes(
        string written,
        string canonical)
    {
        // XKeysymDB used an underscore the headers never had, and libxkbcommon still strips it, so
        // symbols/xfree86 writing XF86_Switch_VT_1 is a keysym rather than a typo. Both spellings
        // have to reach the same verdict; NotRepresentable rather than NotAKeysym is the whole
        // point, because it is the difference between "your model has no volume key" and "this
        // file is broken".
        Assert.Equal(XkbKeysymDecodeOutcome.NotRepresentable, decoder.Decode(written).Outcome);
        Assert.Equal(decoder.Decode(canonical).Outcome, decoder.Decode(written).Outcome);

        // The diagnostic quotes what the file wrote, not the name the lookup was rewritten to.
        Assert.Contains(written, decoder.Decode(written).Diagnostic!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("a", "a")]
    [InlineData("A", "A")]
    [InlineData("1", "1")]
    [InlineData("exclam", "!")]
    [InlineData("aogonek", "ą")]
    [InlineData("eacute", "é")]
    [InlineData("EuroSign", "€")]
    [InlineData("Cyrillic_zhe", "ж")]
    public void Decode_ForANamedCharacter_ReadsItThroughTheTable(string keysym, string expected)
    {
        var result = decoder.Decode(keysym);

        Assert.Equal(new CharacterOutput(expected), result.Output);
        Assert.Equal(XkbKeysymDecodeOutcome.Character, result.Outcome);
        Assert.Null(result.Diagnostic);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("U0105", "ą")]
    [InlineData("U00E9", "é")]
    [InlineData("U192", "ƒ")]
    [InlineData("U3A9", "Ω")]
    [InlineData("U41", "A")]
    [InlineData("0x01000105", "ą")]
    [InlineData("0x010020AC", "€")]
    [InlineData("0x0041", "A")]
    [InlineData("0x00e9", "é")]
    public void Decode_ForACharacterSpeltOutNumerically_ReadsItWithoutTheTable(
        string keysym,
        string expected)
    {
        var result = decoder.Decode(keysym);

        Assert.Equal(new CharacterOutput(expected), result.Output);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decode_ForACharacterOutsideTheBasicPlane_KeepsTheWholeScalar()
    {
        var result = decoder.Decode("0x0101F600");

        Assert.Equal(new CharacterOutput("\U0001F600"), result.Output);
        Assert.Null(result.Diagnostic);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Escape", LogicalKey.Escape)]
    [InlineData("BackSpace", LogicalKey.Backspace)]
    [InlineData("Tab", LogicalKey.Tab)]
    [InlineData("Return", LogicalKey.Enter)]
    [InlineData("space", LogicalKey.Space)]
    [InlineData("F11", LogicalKey.F11)]
    [InlineData("Prior", LogicalKey.PageUp)]
    [InlineData("ISO_Level3_Shift", LogicalKey.RightAlt)]
    [InlineData("KP_7", LogicalKey.Numpad7)]
    [InlineData("Menu", LogicalKey.ContextMenu)]
    public void Decode_ForAKeysymNamingAKey_IsASpecialKey(string keysym, LogicalKey expected)
    {
        var result = decoder.Decode(keysym);

        Assert.Equal(new SpecialKeyOutput(expected), result.Output);
        Assert.Null(result.Diagnostic);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Return")]
    [InlineData("Tab")]
    [InlineData("BackSpace")]
    [InlineData("KP_Multiply")]
    [InlineData("KP_Subtract")]
    public void Decode_ForAKeyWhoseKeysymAlsoNamesACharacter_PrefersTheKey(string keysym)
    {
        // Upstream annotates Return as U+000D and KP_Multiply as U+002A. Reading the character
        // table first would import the Enter key as an invisible control character and the keypad's
        // multiply key as a stray asterisk.
        Assert.True(XkbKeysymTable.TryGetCodepoint(keysym, out _));
        Assert.IsType<SpecialKeyOutput>(decoder.Decode(keysym).Output);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("a")]
    [InlineData("z")]
    [InlineData("7")]
    public void Decode_ForALetterOrDigit_IsACharacterAndNotAKeyName(string keysym)
    {
        // The mirror of the case above. XkbKeysymMapper writes "a" for LogicalKey.A as well as for
        // the character, so an over-eager inverse would turn every Dvorak key back into its
        // physical position and lose the layout.
        Assert.IsType<CharacterOutput>(decoder.Decode(keysym).Output);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("KP_End", LogicalKey.Numpad1)]
    [InlineData("KP_Begin", LogicalKey.Numpad5)]
    [InlineData("KP_Delete", LogicalKey.NumpadDecimal)]
    [InlineData("Page_Down", LogicalKey.PageDown)]
    [InlineData("Alt_R", LogicalKey.RightAlt)]
    [InlineData("Mode_switch", LogicalKey.RightAlt)]
    public void Decode_ForAnInboundOnlyAlias_StillNamesTheKey(string keysym, LogicalKey expected)
    {
        // Generation never writes these, but layouts do. The keypad names matter most: xkeyboard
        // config puts KP_End at level 1 and KP_1 at level 2, so a decoder that only knew KP_1 would
        // drop the whole keypad.
        Assert.Equal(new SpecialKeyOutput(expected), decoder.Decode(keysym).Output);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("dead_acute")]
    [InlineData("dead_circumflex")]
    [InlineData("dead_greek")]
    public void Decode_ForADeadKey_IsNoOutputAndReportsKsi031(string keysym)
    {
        var result = decoder.Decode(keysym, "KeyQ", ModifierLayer.AltGr);

        Assert.IsType<NoOutput>(result.Output);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(XkbKeysymDecodeOutcome.DeadKey, result.Outcome);
        Assert.Equal(LayoutImportDiagnosticCodes.DeadKeyDropped, result.Diagnostic.Code);
        Assert.Equal(ValidationSeverity.Warning, result.Diagnostic.Severity);
        Assert.Equal("KeyQ", result.Diagnostic.KeyId);
        Assert.Equal(ModifierLayer.AltGr, result.Diagnostic.Layer);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("XF86AudioPlay")]
    [InlineData("Multi_key")]
    [InlineData("Hyper_L")]
    public void Decode_ForAKeysymTheModelCannotHold_IsNoOutputAndReportsKsi032(string keysym)
    {
        var result = decoder.Decode(keysym, "KeyF");

        Assert.IsType<NoOutput>(result.Output);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(XkbKeysymDecodeOutcome.NotRepresentable, result.Outcome);
        Assert.Equal(LayoutImportDiagnosticCodes.OutputNotRepresentable, result.Diagnostic.Code);
        Assert.Contains("no equivalent in this model", result.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("not_a_keysym")]
    [InlineData("U00GG")]
    [InlineData("0xZZZZ")]
    [InlineData("U+0105")]
    [InlineData("U0105x")]
    [InlineData("U123456789")]
    public void Decode_ForTextThatIsNotAKeysym_SaysSoRatherThanClaimingItIsUnsupported(string keysym)
    {
        // Same code, different message. A user can act on "the file wrote something I do not
        // understand" but not on "this model does not support it".
        //
        // U+0105 belongs here rather than among the characters: libxkbcommon's own parser takes
        // hex digits straight after the U and nothing else, so accepting the plus would have this
        // importer read a layout the user's machine would reject.
        var result = decoder.Decode(keysym);

        Assert.IsType<NoOutput>(result.Output);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(XkbKeysymDecodeOutcome.NotAKeysym, result.Outcome);
        Assert.Equal(LayoutImportDiagnosticCodes.OutputNotRepresentable, result.Diagnostic.Code);
        Assert.Contains("not recognised as a keysym", result.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("0x0000")]
    [InlineData("0x01000007")]
    public void Decode_ForAControlCharacter_IsNoOutputRatherThanAnInvisibleKey(string keysym)
    {
        var result = decoder.Decode(keysym);

        Assert.IsType<NoOutput>(result.Output);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(LayoutImportDiagnosticCodes.OutputNotRepresentable, result.Diagnostic.Code);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decode_ForSurroundingWhitespace_ReadsTheKeysymAnyway()
    {
        Assert.Equal(new CharacterOutput("a"), decoder.Decode("  a  ").Output);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decode_ForEverythingTheMapperCanWrite_ReturnsTheOutputItWasWrittenFrom()
    {
        // The round trip that keeps generation and import in agreement. Every LogicalKey the
        // generator emits a keysym for has to come back as that same key, or a project exported to
        // XKB and imported again would not be the project that was exported.
        var mapper = new XkbKeysymMapper();
        var mismatches = new List<string>();

        foreach (var logicalKey in Enum.GetValues<LogicalKey>())
        {
            if (!mapper.TryMap(logicalKey, out var keysym))
            {
                continue;
            }

            var output = decoder.Decode(keysym).Output;

            // The mapper is many-to-one in one place — Backslash, InternationalBackslash and
            // InternationalHash all write backslash — so the round trip is asserted on the keysym,
            // not on the key.
            if (!(output is SpecialKeyOutput special && mapper.TryMap(special.Key, out var again) && again == keysym)
                && !(output is CharacterOutput character && mapper.TryMap(character, out var same) && same == keysym))
            {
                mismatches.Add($"{logicalKey} -> {keysym} -> {output}");
            }
        }

        Assert.Empty(mismatches);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decode_ForEveryCharacterTheMapperCanWrite_ReturnsThatCharacter()
    {
        var mapper = new XkbKeysymMapper();
        var mismatches = new List<string>();

        // Every printable ASCII and Latin-1 character, which is the range the mapper names
        // explicitly and where a wrong keysym name would be most visible.
        foreach (var codepoint in Enumerable.Range(0x21, 0x5E).Concat(Enumerable.Range(0xA1, 0x5F)))
        {
            var character = new CharacterOutput(char.ConvertFromUtf32(codepoint));
            if (!mapper.TryMap(character, out var keysym))
            {
                continue;
            }

            if (decoder.Decode(keysym).Output is not CharacterOutput decoded || decoded != character)
            {
                mismatches.Add($"U+{codepoint:X4} -> {keysym}");
            }
        }

        Assert.Empty(mismatches);
    }
}
