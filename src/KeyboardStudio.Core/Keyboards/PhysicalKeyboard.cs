namespace KeyboardStudio.Core;

public sealed class PhysicalKeyboard
{
    public required string Id { get; init; }
    public List<PhysicalKey> Keys { get; init; } = [];
}
