namespace KeyboardStudio.Persistence;

internal sealed class PhysicalKeyboardDto
{
    public required string Id { get; init; }
    public required List<PhysicalKeyDto> Keys { get; init; }
}
