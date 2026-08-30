namespace KeyboardStudio.Persistence;

internal sealed class LayoutDerivationMappingDto
{
    public required string KeyId { get; init; }

    public required string LogicalKey { get; init; }

    public Dictionary<string, KeyOutputDto> Outputs { get; init; } = [];

    public bool IsSafeToOverride { get; init; }
}
