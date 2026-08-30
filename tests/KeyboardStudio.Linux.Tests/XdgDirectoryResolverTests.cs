using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XdgDirectoryResolverTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenXdgVariablesAreAbsent_UsesTheHomeFallbackWithoutRequiringDirectories()
    {
        var result = new XdgDirectoryResolver(
            new FakeXkbEnvironment().Set("HOME", "/home/test")).Resolve();

        Assert.True(result.Success);
        Assert.Equal("/home/test/.config/xkb", result.Paths!.UserXkbRoot);
        Assert.Equal(
            "/home/test/.local/state/keyboardstudio/xkb",
            result.Paths.KeyboardStudioStateRoot);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenAbsoluteXdgVariablesAreSet_UsesThem()
    {
        var environment = new FakeXkbEnvironment()
            .Set("HOME", "/home/test")
            .Set("XDG_CONFIG_HOME", "/var/user-config")
            .Set("XDG_STATE_HOME", "/var/user-state");

        var result = new XdgDirectoryResolver(environment).Resolve();

        Assert.Equal("/var/user-config/xkb", result.Paths!.UserXkbRoot);
        Assert.Equal(
            "/var/user-state/keyboardstudio/xkb",
            result.Paths.KeyboardStudioStateRoot);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    [InlineData("XDG_CONFIG_HOME")]
    [InlineData("XDG_STATE_HOME")]
    public void Resolve_WhenAnXdgHomeIsRelative_RejectsIt(string variable)
    {
        var environment = new FakeXkbEnvironment()
            .Set("HOME", "/home/test")
            .Set(variable, "relative/path");

        var result = new XdgDirectoryResolver(environment).Resolve();

        Assert.False(result.Success);
        Assert.Null(result.Paths);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSC002");
    }
}
