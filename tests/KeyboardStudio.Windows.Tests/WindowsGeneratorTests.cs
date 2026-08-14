using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsGeneratorTests
{
    [Fact]
    public async Task Generator_ProducesDeterministicUnicodeMappingSource()
    {
        var project = DemoProjectFactory.Create();
        new KeyboardEditor(project).MapCharacter("KeyA", ModifierLayer.AltGr, "ą");
        var generator = new WindowsArtifactGenerator();
        var options = new BuildOptions(BuildTarget.WindowsX64, "out");

        var first = await generator.GenerateAsync(project, options);
        var second = await generator.GenerateAsync(project, options);

        var firstSource = first.Source.Files["keyboard.c"];
        var secondSource = second.Source.Files["keyboard.c"];
        Assert.Equal(firstSource, secondSource);
        Assert.Contains("0x00000105", firstSource);
        Assert.Contains("KbdLayerDescriptor", firstSource);
    }
}
