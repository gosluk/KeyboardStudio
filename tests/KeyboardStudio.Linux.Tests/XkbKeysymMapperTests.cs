using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// The keysym each logical key is written as.
///
/// The properties worth asserting here are about the table as a whole rather than about any one
/// row. Two keys the model tells apart have to stay apart on the way out, or generation quietly
/// merges them and no amount of correct behaviour on either side puts them back.
/// </summary>
public sealed class XkbKeysymMapperTests
{
    private readonly XkbKeysymMapper mapper = new();

    /// <summary>
    /// The one place the mapper is deliberately many-to-one. All three name the key XKB calls
    /// <c>backslash</c>; which of them a board actually has is a property of its geometry, and the
    /// keysym cannot record a distinction the keysym vocabulary does not make.
    /// </summary>
    private static readonly LogicalKey[] BackslashFamily =
    [
        LogicalKey.Backslash,
        LogicalKey.InternationalBackslash,
        LogicalKey.InternationalHash
    ];

    [Fact]
    [Trait("Category", "Unit")]
    public void TryMap_ForNumpadEnter_WritesKpEnterRatherThanCollapsingIntoReturn()
    {
        // Regression: NumpadEnter was written as Return, which is the main Enter key. Applications
        // that tell the two apart — a terminal, a spreadsheet — saw the wrong key, and a layout
        // exported and imported again came back with its numpad Enter turned into an ordinary one.
        Assert.True(mapper.TryMap(LogicalKey.NumpadEnter, out var numpadEnter));
        Assert.Equal("KP_Enter", numpadEnter);

        Assert.True(mapper.TryMap(LogicalKey.Enter, out var enter));
        Assert.Equal("Return", enter);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryMap_ForTwoKeysTheModelTellsApart_DoesNotWriteOneKeysymForBoth()
    {
        // The general form of the case above, and the one that catches the next such collapse
        // rather than the last one. Every logical key that shares a keysym with another is listed
        // as a mismatch unless the two are the documented backslash family.
        var byKeysym = new Dictionary<string, List<LogicalKey>>(StringComparer.Ordinal);

        foreach (var logicalKey in Enum.GetValues<LogicalKey>())
        {
            if (logicalKey is LogicalKey.None || !mapper.TryMap(logicalKey, out var keysym))
            {
                continue;
            }

            if (!byKeysym.TryGetValue(keysym, out var keys))
            {
                keys = [];
                byKeysym[keysym] = keys;
            }

            keys.Add(logicalKey);
        }

        var collapsed = byKeysym
            .Where(entry => entry.Value.Count > 1 && entry.Value.Except(BackslashFamily).Any())
            .Select(entry => $"{entry.Key} is written for {string.Join(", ", entry.Value)}")
            .ToArray();

        Assert.Empty(collapsed);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(LogicalKey.NumpadEnter, LogicalKey.Enter)]
    [InlineData(LogicalKey.NumpadAdd, LogicalKey.Equal)]
    [InlineData(LogicalKey.NumpadDecimal, LogicalKey.Period)]
    [InlineData(LogicalKey.NumpadDivide, LogicalKey.Slash)]
    [InlineData(LogicalKey.NumpadSubtract, LogicalKey.Minus)]
    [InlineData(LogicalKey.Numpad0, LogicalKey.Digit0)]
    public void TryMap_ForAKeypadKey_WritesSomethingOtherThanItsMainKeyboardTwin(
        LogicalKey keypadKey,
        LogicalKey mainKey)
    {
        // The keypad is where a collapse is easiest to write and hardest to notice: the two keys
        // type the same character, so nothing on screen looks wrong until an application asks
        // which key was pressed.
        Assert.True(mapper.TryMap(keypadKey, out var keypad));
        Assert.True(mapper.TryMap(mainKey, out var main));

        Assert.NotEqual(main, keypad);
        Assert.StartsWith("KP_", keypad, StringComparison.Ordinal);
    }
}
