using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbUserBundleWriterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_WritesOnlyBeneathTheBuildOutputBundleDirectory()
    {
        var output = Path.Combine(Path.GetTempPath(), $"keyboardstudio-bundle-{Guid.NewGuid():N}");
        try
        {
            var bundle = new XkbGeneratedUserBundle(
            [
                new XkbUserBundleFile("symbols/keyboardstudio", "symbols\n", "hash-a"),
                new XkbUserBundleFile("rules/evdev.xml", "registry\n", "hash-b")
            ]);

            var result = await new XkbUserBundleWriter().WriteAsync(bundle, output);

            Assert.Equal(Path.Combine(output, "xkb-user-bundle"), result.BundleRoot);
            Assert.Equal("symbols\n", await File.ReadAllTextAsync(
                Path.Combine(result.BundleRoot, "symbols", "keyboardstudio")));
            Assert.Equal("registry\n", await File.ReadAllTextAsync(
                Path.Combine(result.BundleRoot, "rules", "evdev.xml")));
            Assert.All(result.WrittenPaths, path => Assert.StartsWith(result.BundleRoot, path));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task WriteAsync_WhenAPathTraversesOutOfTheBundle_RejectsIt()
    {
        var output = Path.Combine(Path.GetTempPath(), $"keyboardstudio-bundle-{Guid.NewGuid():N}");
        var bundle = new XkbGeneratedUserBundle(
            [new XkbUserBundleFile("../outside", "bad", "hash")]);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new XkbUserBundleWriter().WriteAsync(bundle, output));

        Assert.False(File.Exists(Path.Combine(output, "outside")));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    [InlineData("/etc/xkb")]
    [InlineData("C:\\xkb\\symbols")]
    public async Task WriteAsync_WhenAPathIsRootedOrPlatformSpecific_RejectsIt(string relativePath)
    {
        var output = Path.Combine(Path.GetTempPath(), $"keyboardstudio-bundle-{Guid.NewGuid():N}");
        var bundle = new XkbGeneratedUserBundle(
            [new XkbUserBundleFile(relativePath, "bad", "hash")]);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new XkbUserBundleWriter().WriteAsync(bundle, output));
    }
}
