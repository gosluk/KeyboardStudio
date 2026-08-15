using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsLayoutTranslatorScanCodeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenProjectContainsNormalAndExtendedKeys_SeparatesScanCodeTables()
    {
        var project = CreateProject(
            new PhysicalKey { Id = "KeyA", ScanCode = 0x1E },
            LogicalKey.A,
            new PhysicalKey { Id = "ArrowLeft", ScanCode = 0x4B, Extended = true },
            LogicalKey.ArrowLeft);

        var layout = WindowsLayoutTranslator.Translate(project);

        Assert.Equal(
            [new VscToVkMapping(0x1E, WindowsVirtualKey.A)],
            layout.VscToVkMappings);
        Assert.Equal(
            [new ExtendedVscToVkMapping(0x4B, WindowsVirtualKey.Left)],
            layout.ExtendedVscToVkMappings);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Translate_WhenMappingsAreOutOfOrder_OrdersEachScanCodeTableDeterministically()
    {
        var project = CreateProject(
            new PhysicalKey { Id = "KeyB", ScanCode = 0x30 },
            LogicalKey.B,
            new PhysicalKey { Id = "KeyA", ScanCode = 0x1E },
            LogicalKey.A);

        var layout = WindowsLayoutTranslator.Translate(project);

        Assert.Equal([0x1E, 0x30], layout.VscToVkMappings.Select(mapping => mapping.ScanCode));
    }

    private static KeyboardProject CreateProject(
        PhysicalKey firstKey,
        LogicalKey firstLogicalKey,
        PhysicalKey secondKey,
        LogicalKey secondLogicalKey) =>
        new()
        {
            Metadata = new ProjectMetadata
            {
                Name = "Translation test",
                Description = "Translation test",
                Version = "1.0.0",
                Language = "en-US"
            },
            Keyboard = new PhysicalKeyboard
            {
                Id = "translation-test",
                Keys = [firstKey, secondKey]
            },
            Layout = new KeyboardLayout
            {
                Mappings =
                [
                    new KeyMapping { KeyId = firstKey.Id, LogicalKey = firstLogicalKey },
                    new KeyMapping { KeyId = secondKey.Id, LogicalKey = secondLogicalKey }
                ]
            }
        };
}
