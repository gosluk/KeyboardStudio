using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Testing;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsArtifactOutputNameTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateAsync_DerivesSafeDllNameFromLayoutMetadata()
    {
        var generator = new WindowsArtifactGenerator(new WindowsLayoutMetadata(
            "Demo Layout/International",
            "Demo layout"));

        var artifact = await generator.GenerateAsync(
            TestProjectFactory.Create(),
            new BuildOptions(BuildTarget.WindowsX64, "out"));

        Assert.Equal("demo_layout_international.dll", artifact.OutputFileName);
    }
}
