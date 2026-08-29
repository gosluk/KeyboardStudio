using KeyboardStudio.Core;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Seed source that produces bare geometry with no mappings.
/// </summary>
/// <remarks>
/// A shipped document is never empty, so tests that exercise unmapped-key behaviour — keycap
/// legends, layer counts, the "no logical key" diagnostic — have to ask for an empty project
/// explicitly rather than relying on what a new document happens to contain.
/// </remarks>
internal sealed class EmptySeedProjectSource : ISeedProjectSource
{
    private readonly KeyboardTemplateProvider _templateProvider = new();

    public IReadOnlyList<string> SeedIds { get; } = [SeedProjectId.UsBasic];

    public KeyboardProject Create(string seedId) => new()
    {
        Metadata = new ProjectMetadata
        {
            Name = "Empty layout",
            Description = "Bare geometry used by tests that need unmapped keys.",
            Version = "0.1.0",
            Language = "und"
        },
        Keyboard = _templateProvider.Load("iso-105"),
        Layout = new KeyboardLayout()
    };
}
