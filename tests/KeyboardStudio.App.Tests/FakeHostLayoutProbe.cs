using KeyboardStudio.Core;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// A probe that answers with whatever the test put in it.
///
/// Its default answer is nothing, which is what every test that is not about the startup import
/// wants: those build a view model on a machine whose real configuration is none of their business,
/// and a probe that detected the test host's own layout would make them depend on it.
/// </summary>
internal sealed class FakeHostLayoutProbe : IHostLayoutProbe
{
    private readonly ImportableLayoutReference? _reference;

    public FakeHostLayoutProbe(ImportableLayoutReference? reference) => _reference = reference;

    public static FakeHostLayoutProbe Detecting(string layoutId, string? variantId = null) =>
        new(new ImportableLayoutReference("fake", layoutId, variantId));

    public int DetectCount { get; private set; }

    public ImportableLayoutReference? Detect()
    {
        DetectCount++;
        return _reference;
    }
}
