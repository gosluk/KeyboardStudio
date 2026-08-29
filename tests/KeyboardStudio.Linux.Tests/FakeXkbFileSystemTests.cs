using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// The fake filesystem every import test is written against.
///
/// A test double is worth testing only where getting it wrong would make the suite lie, and this
/// one has exactly that property: it is keyed by path string, so a host whose separator differs
/// from the one the tests write turns every lookup into a miss and every import test into a
/// failure that says nothing about the importer.
/// </summary>
public sealed class FakeXkbFileSystemTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void FileExists_ForThePathWindowsWouldCompose_FindsTheFileTheTestWrote()
    {
        // Path.Combine("/usr/share/X11/xkb", "symbols") yields a backslash on Windows and a forward
        // slash on Linux, so this is the one case a Linux-only run would never reach. Spelling the
        // backslash form out keeps the fix verifiable on either host rather than only on the one
        // that broke.
        var fileSystem = new FakeXkbFileSystem()
            .AddFile("/usr/share/X11/xkb/symbols/pl", "xkb_symbols \"basic\" {};");

        Assert.True(fileSystem.FileExists(@"/usr/share/X11/xkb\symbols\pl"));
        Assert.True(fileSystem.DirectoryExists(@"/usr/share/X11/xkb\symbols"));
        Assert.Equal(
            ["/usr/share/X11/xkb/symbols/pl"],
            fileSystem.EnumerateFiles(@"/usr/share/X11/xkb\symbols"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddFile_ForAFileDeepInATree_ImpliesEveryDirectoryAboveIt()
    {
        var fileSystem = new FakeXkbFileSystem()
            .AddFile("/usr/share/X11/xkb/rules/evdev.xml", "<xkbConfigRegistry/>");

        Assert.True(fileSystem.DirectoryExists("/usr/share/X11/xkb/rules"));
        Assert.True(fileSystem.DirectoryExists("/usr/share"));
        Assert.True(fileSystem.DirectoryExists("/"));

        // The walk up stops at the root rather than looping or inventing a parent above it.
        Assert.False(fileSystem.DirectoryExists("/usr/share/X11/xkb/rules/evdev.xml"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EnumerateFiles_ForADirectory_ReturnsOnlyItsOwnChildrenInOrder()
    {
        var fileSystem = new FakeXkbFileSystem()
            .AddFile("/xkb/symbols/us", "")
            .AddFile("/xkb/symbols/pl", "")
            .AddFile("/xkb/symbols/nested/de", "")
            .AddFile("/xkb/rules/evdev.xml", "");

        Assert.Equal(["/xkb/symbols/pl", "/xkb/symbols/us"], fileSystem.EnumerateFiles("/xkb/symbols"));
    }
}
