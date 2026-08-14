using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsGeneratorTests
{
    [Fact]
    public async Task Generate_WhenInputIsIdentical_ProducesIdenticalUnicodeMappingSource()
    {
        var project = DemoProjectFactory.Create();
        new KeyboardEditor(project).MapCharacter("KeyA", ModifierLayer.AltGr, "ą");
        var metadata = new WindowsLayoutMetadata("kbd-demo", "Demo layout");
        var generator = new WindowsArtifactGenerator(metadata);
        var options = new BuildOptions(BuildTarget.WindowsX64, "out");

        var first = await generator.GenerateAsync(project, options);
        var second = await generator.GenerateAsync(project, options);

        Assert.Equal(
            ["keyboard.c", "keyboard.def", "keyboard.h", "keyboard.rc"],
            first.Source.Files.Keys);
        Assert.Equal(first.Source.Files.Keys, second.Source.Files.Keys);
        Assert.All(first.Source.Files, file => Assert.Equal(file.Value, second.Source.Files[file.Key]));

        var firstSource = first.Source.Files["keyboard.c"];
        var secondSource = second.Source.Files["keyboard.c"];
        Assert.Equal(firstSource, secondSource);
        Assert.Contains("Layout ID: kbd-demo", firstSource);
        Assert.Contains("Layout name: Demo layout", firstSource);
        Assert.Contains("0x00000105", firstSource);
        Assert.Contains("KbdLayerDescriptor", firstSource);
    }

    [Fact]
    public async Task Generate_WhenLayoutHasNormalAndExtendedKeys_EmitsNativeScanCodeTables()
    {
        var project = DemoProjectFactory.Create();
        project.Keyboard.Keys.Add(new PhysicalKey
        {
            Id = "ArrowLeft",
            ScanCode = 0x4B,
            Extended = true
        });
        project.Layout.Mappings.Add(new KeyMapping
        {
            KeyId = "ArrowLeft",
            LogicalKey = LogicalKey.ArrowLeft
        });

        var artifact = await new WindowsArtifactGenerator(
                new WindowsLayoutMetadata("kbd-demo", "Demo layout"))
            .GenerateAsync(project, new BuildOptions(BuildTarget.WindowsX64, "out"));
        var source = artifact.Source.Files["keyboard.c"];

        Assert.Contains("static ALLOC_SECTION_LDATA USHORT ausVscToVk[]", source);
        Assert.Contains("0x0041 /* 0x1E */", source);
        Assert.Contains("{ 0x4B, 0x0025 | KBDEXT }", source);
        Assert.Contains("static ALLOC_SECTION_LDATA VSC_VK aE1VscToVk[]", source);
        Assert.Contains("{ 0x00, 0x0000 }", source);
        Assert.Contains("static ALLOC_SECTION_LDATA VSC_LPWSTR aKeyNamesExt[]", source);
        Assert.Contains("{ 0x4B, L\"Left\" }", source);
        Assert.DoesNotContain("L\"ArrowLeft\"", source);
    }
}
