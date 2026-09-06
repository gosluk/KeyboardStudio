namespace KeyboardStudio.Persistence;

internal sealed class LayoutDerivationMappingDto
{
    public required string KeyId { get; init; }

    public required string LogicalKey { get; init; }

    public Dictionary<string, KeyOutputDto> Outputs { get; init; } = [];

    public bool IsSafeToOverride { get; init; }

    /// <summary>
    /// The source's own levels for this key, verbatim, in level order. Optional within schema 3: a
    /// derivation written before it was kept simply has none, and its keys stay as safe or unsafe
    /// to override as they were recorded.
    /// </summary>
    public List<string> SourceLevels { get; init; } = [];

    /// <summary>The key type the source declared, when it declared one.</summary>
    public string? SourceKeyType { get; init; }
}
