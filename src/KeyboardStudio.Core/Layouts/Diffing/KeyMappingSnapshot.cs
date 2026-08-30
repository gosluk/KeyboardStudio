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
        bool isSafeToOverride = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(outputs);

        KeyId = keyId;
        LogicalKey = logicalKey;
        Outputs = outputs.ToFrozenDictionary(pair => pair.Key, pair => pair.Value);
        IsSafeToOverride = isSafeToOverride;
    }

    public string KeyId { get; }

    public LogicalKey LogicalKey { get; }

    public IReadOnlyDictionary<ModifierLayer, KeyOutput> Outputs { get; }

    /// <summary>
    /// Whether replacing this key's complete supported mapping cannot erase source behavior the
    /// importer had to drop or approximate.
    /// </summary>
    public bool IsSafeToOverride { get; }

    public static KeyMappingSnapshot From(KeyMapping mapping, bool isSafeToOverride = true)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return new KeyMappingSnapshot(
            mapping.KeyId,
            mapping.LogicalKey,
            mapping.Outputs,
            isSafeToOverride);
    }
}
