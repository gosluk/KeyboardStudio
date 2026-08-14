using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsCharacterTableTests
{
    public static TheoryData<LogicalKey, WindowsVirtualKey, char, char, WindowsCharacterAttributes>
        RepresentativeCharacterRows => new()
        {
            { LogicalKey.A, WindowsVirtualKey.A, 'a', 'A', WindowsCharacterAttributes.CapsLock },
            { LogicalKey.Digit1, WindowsVirtualKey.Digit1, '1', '!', WindowsCharacterAttributes.None },
            { LogicalKey.Semicolon, WindowsVirtualKey.Oem1, ';', ':', WindowsCharacterAttributes.None },
            { LogicalKey.Space, WindowsVirtualKey.Space, ' ', ' ', WindowsCharacterAttributes.None }
        };

    [Theory]
    [MemberData(nameof(RepresentativeCharacterRows))]
    public void Translate_WhenCharacterLayersArePresent_CreatesTypedRow(
        LogicalKey logicalKey,
        WindowsVirtualKey expectedVirtualKey,
        char normal,
        char shift,
        WindowsCharacterAttributes expectedAttributes)
    {
        var project = CreateProject(logicalKey, normal, shift);

        var table = WindowsLayoutTranslator.Translate(project).Characters;

        var row = Assert.Single(table.Rows);
        Assert.Equal(2, table.Width);
        Assert.Equal(expectedVirtualKey, row.VirtualKey);
        Assert.Equal(expectedAttributes, row.Attributes);
        Assert.Equal(normal, row.Default);
        Assert.Equal(shift, row.Shift);
        Assert.Null(row.AltGr);
        Assert.Null(row.ShiftAltGr);
    }

    [Fact]
    public void Translate_WhenAltGrLayersArePresent_SelectsFourColumnTable()
    {
        var project = CreateProject(LogicalKey.A, 'a', 'A');
        var mapping = Assert.Single(project.Layout.Mappings);
        mapping.Outputs[ModifierLayer.AltGr] = new CharacterOutput("ą");
        mapping.Outputs[ModifierLayer.ShiftAltGr] = new CharacterOutput("Ą");

        var table = WindowsLayoutTranslator.Translate(project).Characters;

        var row = Assert.Single(table.Rows);
        Assert.Equal(4, table.Width);
        Assert.Equal('ą', row.AltGr);
        Assert.Equal('Ą', row.ShiftAltGr);
    }

    private static KeyboardProject CreateProject(LogicalKey logicalKey, char normal, char shift) =>
        new()
        {
            Metadata = new ProjectMetadata
            {
                Name = "Character translation test",
                Description = "Character translation test",
                Version = "1.0.0",
                Language = "en-US"
            },
            Keyboard = new PhysicalKeyboard
            {
                Id = "character-translation-test",
                Keys = [new PhysicalKey { Id = "TestKey", ScanCode = 0x1E }]
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
                            [ModifierLayer.Default] = new CharacterOutput(normal.ToString()),
                            [ModifierLayer.Shift] = new CharacterOutput(shift.ToString())
                        }
                    }
                ]
            }
        };
}
