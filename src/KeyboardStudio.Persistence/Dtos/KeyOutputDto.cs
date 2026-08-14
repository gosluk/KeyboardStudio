namespace KeyboardStudio.Persistence;

internal sealed class KeyOutputDto
{
    public required string Kind { get; init; }
    public string? Value { get; init; }
    public string? Key { get; init; }
}
