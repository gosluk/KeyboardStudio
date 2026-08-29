using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbDataRootLocatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Locate_OnAHostWithEveryRootPresent_ReturnsLibxkbcommonSearchOrder()
    {
        var environment = new FakeXkbEnvironment()
            .Set("XKB_CONFIG_ROOT", "/opt/xkb")
            .Set("XDG_CONFIG_HOME", "/home/ada/.config");
        var fileSystem = new FakeXkbFileSystem()
            .AddDirectory("/opt/xkb")
            .AddDirectory("/home/ada/.config/xkb")
            .AddDirectory("/etc/xkb")
            .AddDirectory("/usr/share/X11/xkb")
            .AddDirectory("/usr/local/share/X11/xkb");

        var roots = new XkbDataRootLocator(environment, fileSystem).Locate();

        Assert.Equal(
            [
                "/opt/xkb",
                "/home/ada/.config/xkb",
                "/etc/xkb",
                "/usr/share/X11/xkb",
                "/usr/local/share/X11/xkb"
            ],
            roots.Select(root => root.Path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Locate_ForTheUsersOwnDirectory_TagsItAsAUserRoot()
    {
        var environment = new FakeXkbEnvironment().Set("HOME", "/home/ada");
        var fileSystem = new FakeXkbFileSystem()
            .AddDirectory("/home/ada/.config/xkb")
            .AddDirectory("/usr/share/X11/xkb");

        var roots = new XkbDataRootLocator(environment, fileSystem).Locate();

        Assert.Equal(
            [
                ("/home/ada/.config/xkb", LayoutSourceOrigin.User),
                ("/usr/share/X11/xkb", LayoutSourceOrigin.System)
            ],
            roots.Select(root => (root.Path, root.Origin)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Locate_WhenXdgConfigHomeIsUnset_FallsBackToHomeDotConfig()
    {
        var environment = new FakeXkbEnvironment().Set("HOME", "/home/ada");
        var fileSystem = new FakeXkbFileSystem().AddDirectory("/home/ada/.config/xkb");

        var roots = new XkbDataRootLocator(environment, fileSystem).Locate();

        Assert.Equal(["/home/ada/.config/xkb"], roots.Select(root => root.Path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Locate_WhenXdgConfigHomeIsSet_PrefersItOverHome()
    {
        var environment = new FakeXkbEnvironment()
            .Set("XDG_CONFIG_HOME", "/var/config")
            .Set("HOME", "/home/ada");
        var fileSystem = new FakeXkbFileSystem()
            .AddDirectory("/var/config/xkb")
            .AddDirectory("/home/ada/.config/xkb");

        var roots = new XkbDataRootLocator(environment, fileSystem).Locate();

        Assert.Equal(["/var/config/xkb"], roots.Select(root => root.Path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Locate_WhenXdgConfigHomeIsRelative_IgnoresItRatherThanResolvingIt()
    {
        // The base-directory specification calls a relative XDG_CONFIG_HOME invalid. Resolving it
        // against the working directory would point the search at wherever the app happened to be
        // launched from, which has nothing to do with the user's configuration.
        var environment = new FakeXkbEnvironment()
            .Set("XDG_CONFIG_HOME", "relative/config")
            .Set("HOME", "/home/ada");
        var fileSystem = new FakeXkbFileSystem().AddDirectory("/home/ada/.config/xkb");

        var roots = new XkbDataRootLocator(environment, fileSystem).Locate();

        Assert.Equal(["/home/ada/.config/xkb"], roots.Select(root => root.Path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Locate_ForARootThatIsNotInstalled_PassesOverIt()
    {
        var fileSystem = new FakeXkbFileSystem().AddDirectory("/usr/share/X11/xkb");

        var roots = new XkbDataRootLocator(new FakeXkbEnvironment(), fileSystem).Locate();

        Assert.Equal(["/usr/share/X11/xkb"], roots.Select(root => root.Path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Locate_WhenTheConfiguredRootRepeatsASystemRoot_ListsItOnce()
    {
        // Otherwise every layout in that root would appear twice in the catalog.
        var environment = new FakeXkbEnvironment().Set("XKB_CONFIG_ROOT", "/usr/share/X11/xkb/");
        var fileSystem = new FakeXkbFileSystem().AddDirectory("/usr/share/X11/xkb");

        var roots = new XkbDataRootLocator(environment, fileSystem).Locate();

        Assert.Equal(["/usr/share/X11/xkb"], roots.Select(root => root.Path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Locate_OnAHostWithNoXkbDatabase_ReturnsNothingRatherThanFailing()
    {
        // A host without XKB is ordinary — a Windows machine, or a container with no X11 data.
        var roots = new XkbDataRootLocator(new FakeXkbEnvironment(), new FakeXkbFileSystem()).Locate();

        Assert.Empty(roots);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DataRoot_ExposesTheSubdirectoriesTheImportPipelineReads()
    {
        var root = new XkbDataRoot("/usr/share/X11/xkb", LayoutSourceOrigin.System);

        Assert.Equal(Path.Combine("/usr/share/X11/xkb", "rules"), root.RulesDirectory);
        Assert.Equal(Path.Combine("/usr/share/X11/xkb", "symbols"), root.SymbolsDirectory);
    }
}
