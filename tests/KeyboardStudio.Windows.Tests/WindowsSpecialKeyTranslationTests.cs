using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsSpecialKeyTranslationTests
{
    public static TheoryData<LogicalKey> ScanOnlyKeys => new()
    {
        LogicalKey.Enter,
        LogicalKey.Tab,
        LogicalKey.Backspace,
        LogicalKey.Escape,
        LogicalKey.CapsLock,
        LogicalKey.F12,
        LogicalKey.ArrowLeft,
        LogicalKey.NumpadEnter,
        LogicalKey.LeftShift,
        LogicalKey.RightAlt
    };

    public static TheoryData<LogicalKey> CharacterKeys => new()
    {
        LogicalKey.A,
        LogicalKey.Digit1,
        LogicalKey.Semicolon,
        LogicalKey.Space,
        LogicalKey.Numpad1,
        LogicalKey.NumpadAdd
    };

    [Theory]
    [MemberData(nameof(ScanOnlyKeys))]
    public void ProducesCharacters_WhenKeyIsScanOnly_ReturnsFalse(LogicalKey logicalKey)
    {
        Assert.False(WindowsLogicalKeyClassifier.ProducesCharacters(logicalKey));
    }

    [Theory]
    [MemberData(nameof(CharacterKeys))]
    public void ProducesCharacters_WhenKeyIsPrintable_ReturnsTrue(LogicalKey logicalKey)
    {
        Assert.True(WindowsLogicalKeyClassifier.ProducesCharacters(logicalKey));
    }

    [Theory]
    [InlineData(LogicalKey.Enter, WindowsVirtualKey.Return)]
    [InlineData(LogicalKey.Tab, WindowsVirtualKey.Tab)]
    [InlineData(LogicalKey.Backspace, WindowsVirtualKey.Back)]
    public void Translate_WhenKeyIsNonCharacter_CreatesOnlyScanCodeMapping(
        LogicalKey logicalKey,
        WindowsVirtualKey expectedVirtualKey)
    {
        var project = CreateProject(logicalKey, extended: false);

        var layout = WindowsLayoutTranslator.Translate(project);

        Assert.Empty(layout.Characters.Rows);
        var scanCodeMapping = Assert.Single(layout.VscToVkMappings);
        Assert.Equal(expectedVirtualKey, scanCodeMapping.VirtualKey);
    }

    [Fact]
    public void Translate_WhenNavigationKeyIsExtended_CreatesOnlyExtendedScanCodeMapping()
    {
        var project = CreateProject(LogicalKey.ArrowLeft, extended: true);

        var layout = WindowsLayoutTranslator.Translate(project);

        Assert.Empty(layout.Characters.Rows);
        Assert.Empty(layout.VscToVkMappings);
        Assert.Equal(WindowsVirtualKey.Left, Assert.Single(layout.ExtendedVscToVkMappings).VirtualKey);
    }

    private static KeyboardProject CreateProject(LogicalKey logicalKey, bool extended) =>
        new()
        {
            Metadata = new ProjectMetadata
            {
                Name = "Special key translation test",
                Description = "Special key translation test",
                Version = "1.0.0",
                Language = "en-US"
            },
            Keyboard = new PhysicalKeyboard
            {
                Id = "special-key-translation-test",
                Keys = [new PhysicalKey { Id = "TestKey", ScanCode = 0x1C, Extended = extended }]
            },
            Layout = new KeyboardLayout
            {
                Mappings =
                [
                    new KeyMapping
                    {
                        KeyId = "TestKey",
                        LogicalKey = logicalKey,
                        Outputs =
                        {
                            [ModifierLayer.Default] = new SpecialKeyOutput(logicalKey)
                        }
                    }
                ]
            }
        };
}
