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
}
