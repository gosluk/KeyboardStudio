namespace KeyboardStudio.Persistence;

internal sealed class KeyboardLayoutDto
{
    public required List<KeyMappingDto> Mappings { get; init; }
}
