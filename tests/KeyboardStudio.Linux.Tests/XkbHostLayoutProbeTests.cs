using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// The one translation between what a Linux host records and what the import catalog accepts.
/// </summary>
public sealed class XkbHostLayoutProbeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_ReturnsAReferenceTheInstalledLayoutSourceCanResolve()
    {
        var environment = new FakeXkbEnvironment()
            .Set("XKB_DEFAULT_LAYOUT", "pl")
            .Set("XKB_DEFAULT_VARIANT", "qwertz");
        var probe = new XkbHostLayoutProbe(
            new XkbActiveLayoutProbe(environment, new FakeXkbFileSystem()));

        var reference = probe.Detect();

        Assert.NotNull(reference);
        Assert.Equal(XkbLayoutImportSource.SourceId, reference.SourceId);
        Assert.Equal("pl", reference.LayoutId);
        Assert.Equal("qwertz", reference.VariantId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Detect_OnAnUnconfiguredHost_StillNamesALayoutRatherThanNothing()
    {
        // Detection ends at us rather than at null, and us is importable like anything else.
        // Whether this host actually has it is the import's question, not the probe's.
        var probe = new XkbHostLayoutProbe(
            new XkbActiveLayoutProbe(new FakeXkbEnvironment(), new FakeXkbFileSystem()));

        var reference = probe.Detect();

        Assert.NotNull(reference);
        Assert.Equal("us", reference.LayoutId);
        Assert.Null(reference.VariantId);
    }
}
