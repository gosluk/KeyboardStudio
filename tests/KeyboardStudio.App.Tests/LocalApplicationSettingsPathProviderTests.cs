using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class LocalApplicationSettingsPathProviderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void GetSettingsPath_WhenRootIsInjected_ResolvesUnderAKeyboardStudioDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "local-application-data");
        var provider = new LocalApplicationSettingsPathProvider(root);

        var path = provider.GetSettingsPath();

        Assert.Equal(Path.Combine(root, "KeyboardStudio", "settings.json"), path);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSettingsPath_WhenCalledRepeatedly_IsStable()
    {
        var provider = new LocalApplicationSettingsPathProvider(
            Path.Combine(Path.GetTempPath(), "local-application-data"));

        Assert.Equal(provider.GetSettingsPath(), provider.GetSettingsPath());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Constructor_WhenRootIsBlank_Rejects(string root) =>
        Assert.Throws<ArgumentException>(() => new LocalApplicationSettingsPathProvider(root));

    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultConstructor_UsesTheHostLocalApplicationDataDirectory()
    {
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var path = new LocalApplicationSettingsPathProvider().GetSettingsPath();

        Assert.Equal(Path.Combine(expectedRoot, "KeyboardStudio", "settings.json"), path);
    }
}
