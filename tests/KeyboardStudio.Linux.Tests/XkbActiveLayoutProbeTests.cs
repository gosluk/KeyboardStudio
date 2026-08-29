using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// The fallback chain that decides which layout a host is configured to type with. Every case runs
/// against a fake environment and a fake filesystem, because the point of the chain is what happens
/// when a step is absent, and the test host has whatever it has.
/// </summary>
public sealed class XkbActiveLayoutProbeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_WithNothingConfigured_FallsBackToUs()
    {
        var probe = Create(new FakeXkbEnvironment(), new FakeXkbFileSystem());

        var active = probe.Detect();

        Assert.Equal("us", active.LayoutId);
        Assert.Null(active.VariantId);
        Assert.Equal(XkbActiveLayoutOrigin.Fallback, active.Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_WithTheEnvironmentSet_PrefersItOverEveryFile()
    {
        var environment = new FakeXkbEnvironment()
            .Set("XKB_DEFAULT_LAYOUT", "pl")
            .Set("XKB_DEFAULT_VARIANT", "dvorak");
        var fileSystem = new FakeXkbFileSystem()
            .AddFile(XkbActiveLayoutProbe.XorgKeyboardConfigurationPath, XorgConfiguration("de", "neo"))
            .AddFile(XkbActiveLayoutProbe.VirtualConsoleConfigurationPath, "XKBLAYOUT=fr\n");

        var active = Create(environment, fileSystem).Detect();

        Assert.Equal("pl", active.LayoutId);
        Assert.Equal("dvorak", active.VariantId);
        Assert.Equal(XkbActiveLayoutOrigin.Environment, active.Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_WithTheXorgConfiguration_ReadsItsOptionsAndOutranksTheSystemFiles()
    {
        var fileSystem = new FakeXkbFileSystem()
            .AddFile(XkbActiveLayoutProbe.XorgKeyboardConfigurationPath, XorgConfiguration("de", "neo"))
            .AddFile(XkbActiveLayoutProbe.VirtualConsoleConfigurationPath, "XKBLAYOUT=fr\n");

        var active = Create(new FakeXkbEnvironment(), fileSystem).Detect();

        Assert.Equal("de", active.LayoutId);
        Assert.Equal("neo", active.VariantId);
        Assert.Equal(XkbActiveLayoutOrigin.XorgConfiguration, active.Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_WithTheXorgConfiguration_IgnoresCommentedOptionsAndMatchesTheNameCaseInsensitively()
    {
        var fileSystem = new FakeXkbFileSystem().AddFile(
            XkbActiveLayoutProbe.XorgKeyboardConfigurationPath,
            """
            Section "InputClass"
                    Identifier "system-keyboard"
                    MatchIsKeyboard "on"
            #        Option "XkbLayout" "ru"
                    option "xkblayout" "cz"
            EndSection
            """);

        var active = Create(new FakeXkbEnvironment(), fileSystem).Detect();

        Assert.Equal("cz", active.LayoutId);
        Assert.Null(active.VariantId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_WithNoKeyboardOptionInTheXorgConfiguration_FallsThroughToTheSystemFiles()
    {
        // A file that exists but says nothing about the layout is the same as no file at all. It
        // is not an answer of "nothing", and stopping there would strand a host that records its
        // keyboard elsewhere.
        var fileSystem = new FakeXkbFileSystem()
            .AddFile(
                XkbActiveLayoutProbe.XorgKeyboardConfigurationPath,
                """
                Section "InputClass"
                        Identifier "system-keyboard"
                        Option "XkbModel" "pc105"
                EndSection
                """)
            .AddFile(XkbActiveLayoutProbe.VirtualConsoleConfigurationPath, "XKBLAYOUT=fr\n");

        var active = Create(new FakeXkbEnvironment(), fileSystem).Detect();

        Assert.Equal("fr", active.LayoutId);
        Assert.Equal(XkbActiveLayoutOrigin.VirtualConsole, active.Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_WithAnUnreadableXorgConfiguration_FallsThroughRatherThanFailing()
    {
        var fileSystem = new FakeXkbFileSystem()
            .AddUnreadableFile(XkbActiveLayoutProbe.XorgKeyboardConfigurationPath)
            .AddFile(XkbActiveLayoutProbe.VirtualConsoleConfigurationPath, "XKBLAYOUT=fr\n");

        var active = Create(new FakeXkbEnvironment(), fileSystem).Detect();

        Assert.Equal("fr", active.LayoutId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_InTheVirtualConsoleFile_PrefersTheXkbLayoutOverTheConsoleKeymap()
    {
        // KEYMAP names a console keymap, XKBLAYOUT names an XKB layout, and the two vocabularies
        // only sometimes coincide. Where the file carries both, the one that is already in the
        // right vocabulary wins.
        var fileSystem = new FakeXkbFileSystem().AddFile(
            XkbActiveLayoutProbe.VirtualConsoleConfigurationPath,
            """
            # Written by systemd-localed
            KEYMAP=pl2
            XKBLAYOUT="pl"
            XKBVARIANT="qwertz"
            """);

        var active = Create(new FakeXkbEnvironment(), fileSystem).Detect();

        Assert.Equal("pl", active.LayoutId);
        Assert.Equal("qwertz", active.VariantId);
        Assert.Equal(XkbActiveLayoutOrigin.VirtualConsole, active.Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_InTheVirtualConsoleFile_UsesTheConsoleKeymapWhenThatIsAllThereIs()
    {
        var fileSystem = new FakeXkbFileSystem()
            .AddFile(XkbActiveLayoutProbe.VirtualConsoleConfigurationPath, "KEYMAP=uk\nFONT=lat2-16\n");

        var active = Create(new FakeXkbEnvironment(), fileSystem).Detect();

        Assert.Equal("uk", active.LayoutId);
        Assert.Null(active.VariantId);
        Assert.Equal(XkbActiveLayoutOrigin.VirtualConsole, active.Origin);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_WithNothingInTheVirtualConsoleFile_ReadsTheDebianKeyboardDefaults()
    {
        var fileSystem = new FakeXkbFileSystem()
            .AddFile(XkbActiveLayoutProbe.VirtualConsoleConfigurationPath, "FONT=lat2-16\n")
            .AddFile(
                XkbActiveLayoutProbe.KeyboardDefaultsPath,
                """
                XKBMODEL="pc105"
                XKBLAYOUT="gb"
                XKBVARIANT="mac"
                BACKSPACE="guess"
                """);

        var active = Create(new FakeXkbEnvironment(), fileSystem).Detect();

        Assert.Equal("gb", active.LayoutId);
        Assert.Equal("mac", active.VariantId);
        Assert.Equal(XkbActiveLayoutOrigin.KeyboardDefaults, active.Origin);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("us,pl", "", "us", null)]
    [InlineData("us,pl", ",dvorak", "us", null)]
    [InlineData("pl,us", "qwertz,", "pl", "qwertz")]
    [InlineData(" pl , us ", " qwertz , ", "pl", "qwertz")]
    public void Detect_WithSeveralLayoutsConfigured_TakesTheOneTheSessionStartsIn(
        string layouts,
        string variants,
        string expectedLayout,
        string? expectedVariant)
    {
        var environment = new FakeXkbEnvironment()
            .Set("XKB_DEFAULT_LAYOUT", layouts)
            .Set("XKB_DEFAULT_VARIANT", variants);

        var active = Create(environment, new FakeXkbFileSystem()).Detect();

        Assert.Equal(expectedLayout, active.LayoutId);
        Assert.Equal(expectedVariant, active.VariantId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_WithAnEmptyLayoutVariable_TreatsItAsUnsetRatherThanAsAnAnswer()
    {
        var environment = new FakeXkbEnvironment().Set("XKB_DEFAULT_LAYOUT", "   ");
        var fileSystem = new FakeXkbFileSystem()
            .AddFile(XkbActiveLayoutProbe.VirtualConsoleConfigurationPath, "XKBLAYOUT=fr\n");

        var active = Create(environment, fileSystem).Detect();

        Assert.Equal("fr", active.LayoutId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Describe_NamesTheLayoutTheWayXkbDoes()
    {
        Assert.Equal("pl", new XkbActiveLayout("pl", null, XkbActiveLayoutOrigin.Fallback).Describe());
        Assert.Equal(
            "pl(qwertz)",
            new XkbActiveLayout("pl", "qwertz", XkbActiveLayoutOrigin.Fallback).Describe());
    }

    private static XkbActiveLayoutProbe Create(
        FakeXkbEnvironment environment,
        FakeXkbFileSystem fileSystem) =>
        new(environment, fileSystem);

    private static string XorgConfiguration(string layout, string variant) =>
        $"""
        Section "InputClass"
                Identifier "system-keyboard"
                MatchIsKeyboard "on"
                Option "XkbLayout" "{layout}"
                Option "XkbVariant" "{variant}"
        EndSection
        """;
}
