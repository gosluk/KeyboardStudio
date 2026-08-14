namespace KeyboardStudio.Persistence;

internal sealed class KeyboardProjectDto
{
    public int SchemaVersion { get; init; }
    public required ProjectMetadataDto Metadata { get; init; }
    public required PhysicalKeyboardDto Keyboard { get; init; }
    public required KeyboardLayoutDto Layout { get; init; }
}
