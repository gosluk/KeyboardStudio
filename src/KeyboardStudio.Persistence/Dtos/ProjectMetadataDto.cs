namespace KeyboardStudio.Persistence;

internal sealed class ProjectMetadataDto
{
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string Version { get; init; }
    public required string Language { get; init; }
}
