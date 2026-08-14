namespace KeyboardStudio.Core;

public sealed class KeyboardProject
{
    public int SchemaVersion { get; init; } = 1;
    public required ProjectMetadata Metadata { get; init; }
    public required PhysicalKeyboard Keyboard { get; init; }
    public required KeyboardLayout Layout { get; init; }
}

public sealed class ProjectMetadata
{
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Version { get; init; } = "0.1.0";
    public string Language { get; init; } = "und";
}
