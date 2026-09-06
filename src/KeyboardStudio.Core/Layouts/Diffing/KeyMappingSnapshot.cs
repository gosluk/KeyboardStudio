using System.Collections.Frozen;

namespace KeyboardStudio.Core;

/// <summary>
/// An immutable copy of the representable state of one physical key at import time.
/// </summary>
public sealed class KeyMappingSnapshot
{
    public KeyMappingSnapshot(
        string keyId,
        LogicalKey logicalKey,
        IReadOnlyDictionary<ModifierLayer, KeyOutput> outputs,
        bool isSafeToOverride = true,
        IReadOnlyList<string>? sourceLevels = null,
        string? sourceKeyType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(outputs);

        KeyId = keyId;
        LogicalKey = logicalKey;
        Outputs = outputs.ToFrozenDictionary(pair => pair.Key, pair => pair.Value);
        IsSafeToOverride = isSafeToOverride;
        SourceLevels = sourceLevels is null ? [] : [.. sourceLevels];
        SourceKeyType = sourceKeyType;
    }

    public string KeyId { get; }

    public LogicalKey LogicalKey { get; }

    public IReadOnlyDictionary<ModifierLayer, KeyOutput> Outputs { get; }

    /// <summary>
    /// Whether replacing this key's complete supported mapping cannot erase source behavior the
    /// importer had to drop or approximate.
    /// </summary>
    public bool IsSafeToOverride { get; }

    /// <summary>
    /// Every level the source defined for this key, verbatim, or empty when the source could not
    /// report them. See <see cref="LayoutImportKeySource.Levels"/>: what the model could not hold
    /// lives only here, and only a backend that speaks the source format can read it.
    /// </summary>
    public IReadOnlyList<string> SourceLevels { get; }

    /// <summary>The key type the source declared, or <see langword="null"/> when it declared none.</summary>
    public string? SourceKeyType { get; }

    public static KeyMappingSnapshot From(
        KeyMapping mapping,
        bool isSafeToOverride = true,
        LayoutImportKeySource? source = null)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return new KeyMappingSnapshot(
            mapping.KeyId,
            mapping.LogicalKey,
            mapping.Outputs,
            isSafeToOverride,
            source?.Levels,
            source?.KeyType);
    }
}
