namespace KeyboardStudio.Persistence;

internal sealed class KeyMappingDto
{
    public required string KeyId { get; init; }
    public required string LogicalKey { get; init; }
    public required Dictionary<string, KeyOutputDto> Outputs { get; init; }
}
