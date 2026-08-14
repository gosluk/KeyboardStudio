namespace KeyboardStudio.Core;

public sealed class KeyboardProject
{
    public int SchemaVersion { get; init; } = KeyboardProjectSchema.CurrentVersion;
    public required ProjectMetadata Metadata { get; init; }
    public required PhysicalKeyboard Keyboard { get; init; }
    public required KeyboardLayout Layout { get; init; }
}
