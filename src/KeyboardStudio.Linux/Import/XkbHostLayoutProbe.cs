using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Presents the host's configured XKB layout as something the import catalog can fetch.
///
/// The translation is one line, and that is the point: <see cref="IXkbActiveLayoutProbe"/> answers
/// in XKB's vocabulary because that is what it reads, and everything above the composition root
/// works in references. Keeping the two apart is what lets the detection rules be tested against
/// files without a catalog anywhere in sight.
/// </summary>
public sealed class XkbHostLayoutProbe : IHostLayoutProbe
{
    private readonly IXkbActiveLayoutProbe _probe;

    public XkbHostLayoutProbe(IXkbActiveLayoutProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Never null: detection ends at <c>us</c> rather than at nothing, and <c>us</c> is a layout
    /// the source can import like any other. Whether it exists on this host is the import's
    /// question to answer, not this one's.
    /// </remarks>
    public ImportableLayoutReference? Detect()
    {
        var active = _probe.Detect();
        return new ImportableLayoutReference(
            XkbLayoutImportSource.SourceId,
            active.LayoutId,
            active.VariantId);
    }
}
