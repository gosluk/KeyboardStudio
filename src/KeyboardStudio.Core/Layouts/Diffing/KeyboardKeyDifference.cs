namespace KeyboardStudio.Core;

/// <summary>One physical key whose current supported behavior differs from its import baseline.</summary>
public sealed record KeyboardKeyDifference(
    string KeyId,
    KeyboardMappingChangeKind Kind,
    KeyMappingSnapshot? Baseline,
    KeyMappingSnapshot? Current,
    bool LogicalKeyChanged,
    IReadOnlyList<ModifierLayer> ChangedLayers)
{
    public bool IsSafeToOverride => Baseline?.IsSafeToOverride ?? true;
}
