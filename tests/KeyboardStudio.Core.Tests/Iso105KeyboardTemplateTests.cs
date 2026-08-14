using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class Iso105KeyboardTemplateTests
{
    private static readonly KeyboardTemplateProvider Provider = new();

    [Fact]
    public void Load_WhenIso105Requested_ReturnsCompleteExpectedPhysicalKeySet()
    {
        string[] expectedKeyIds =
        [
            "Escape", "F1", "F2", "F3", "F4", "F5",
            "F6", "F7", "F8", "F9", "F10", "F11",
            "F12", "PrintScreen", "ScrollLock", "Pause", "Backquote", "Digit1",
            "Digit2", "Digit3", "Digit4", "Digit5", "Digit6", "Digit7",
            "Digit8", "Digit9", "Digit0", "Minus", "Equal", "Backspace",
            "Insert", "Home", "PageUp", "NumLock", "NumpadDivide", "NumpadMultiply",
            "NumpadSubtract", "Tab", "KeyQ", "KeyW", "KeyE", "KeyR",
            "KeyT", "KeyY", "KeyU", "KeyI", "KeyO", "KeyP",
            "BracketLeft", "BracketRight", "Enter", "Delete", "End", "PageDown",
            "Numpad7", "Numpad8", "Numpad9", "NumpadAdd", "CapsLock", "KeyA",
            "KeyS", "KeyD", "KeyF", "KeyG", "KeyH", "KeyJ",
            "KeyK", "KeyL", "Semicolon", "Quote", "IntlHash", "Numpad4",
            "Numpad5", "Numpad6", "ShiftLeft", "IntlBackslash", "KeyZ", "KeyX",
            "KeyC", "KeyV", "KeyB", "KeyN", "KeyM", "Comma",
            "Period", "Slash", "ShiftRight", "ArrowUp", "Numpad1", "Numpad2",
            "Numpad3", "NumpadEnter", "ControlLeft", "MetaLeft", "AltLeft", "Space",
            "AltRight", "MetaRight", "ContextMenu", "ControlRight", "ArrowLeft", "ArrowDown",
            "ArrowRight", "Numpad0", "NumpadDecimal"
        ];

        var keyboard = Provider.Load("iso-105");
        var actualKeyIds = keyboard.Keys.Select(key => key.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(105, keyboard.Keys.Count);
        Assert.Equal(expectedKeyIds.Length, actualKeyIds.Count);

        foreach (var expectedKeyId in expectedKeyIds)
        {
            Assert.Contains(expectedKeyId, actualKeyIds);
        }
    }

    [Theory]
    [InlineData("Escape", 0x01, false)]
    [InlineData("KeyA", 0x1E, false)]
    [InlineData("IntlHash", 0x2B, false)]
    [InlineData("IntlBackslash", 0x56, false)]
    [InlineData("PrintScreen", 0x37, true)]
    [InlineData("Pause", 0x45, true)]
    [InlineData("Insert", 0x52, true)]
    [InlineData("NumLock", 0x45, false)]
    [InlineData("NumpadDivide", 0x35, true)]
    [InlineData("NumpadEnter", 0x1C, true)]
    [InlineData("ControlLeft", 0x1D, false)]
    [InlineData("ControlRight", 0x1D, true)]
    [InlineData("AltRight", 0x38, true)]
    [InlineData("MetaLeft", 0x5B, true)]
    public void Load_WhenIso105Requested_PreservesScanCodeIdentity(
        string keyId,
        int scanCode,
        bool extended)
    {
        var keyboard = Provider.Load("iso-105");
        var key = Assert.Single(
            keyboard.Keys,
            key => string.Equals(key.Id, keyId, StringComparison.Ordinal));

        Assert.Equal(scanCode, key.ScanCode);
        Assert.Equal(extended, key.Extended);
    }

    [Theory]
    [InlineData("Backspace", 13, 1.5, 2, 1)]
    [InlineData("Tab", 0, 2.5, 1.5, 1)]
    [InlineData("Enter", 13.75, 2.5, 1.25, 2)]
    [InlineData("CapsLock", 0, 3.5, 1.75, 1)]
    [InlineData("ShiftLeft", 0, 4.5, 1.25, 1)]
    [InlineData("ShiftRight", 12.25, 4.5, 2.75, 1)]
    [InlineData("Space", 3.75, 5.5, 6.25, 1)]
    [InlineData("NumpadAdd", 22, 2.5, 1, 2)]
    [InlineData("NumpadEnter", 22, 4.5, 1, 2)]
    [InlineData("Numpad0", 19, 5.5, 2, 1)]
    public void Load_WhenIso105Requested_PreservesRepresentativeGeometry(
        string keyId,
        double x,
        double y,
        double width,
        double height)
    {
        var keyboard = Provider.Load("iso-105");
        var key = Assert.Single(
            keyboard.Keys,
            key => string.Equals(key.Id, keyId, StringComparison.Ordinal));

        Assert.Equal(x, key.X);
        Assert.Equal(y, key.Y);
        Assert.Equal(width, key.Width);
        Assert.Equal(height, key.Height);
    }

    [Fact]
    public void Load_WhenIso105Requested_UsesExpectedRowStartCounts()
    {
        var keyboard = Provider.Load("iso-105");

        Assert.Equal(16, keyboard.Keys.Count(key => key.Y == 0));
        Assert.Equal(21, keyboard.Keys.Count(key => key.Y == 1.5));
        Assert.Equal(21, keyboard.Keys.Count(key => key.Y == 2.5));
        Assert.Equal(16, keyboard.Keys.Count(key => key.Y == 3.5));
        Assert.Equal(18, keyboard.Keys.Count(key => key.Y == 4.5));
        Assert.Equal(13, keyboard.Keys.Count(key => key.Y == 5.5));
    }
}
