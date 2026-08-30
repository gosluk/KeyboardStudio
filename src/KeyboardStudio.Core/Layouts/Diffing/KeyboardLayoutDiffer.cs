using System.Collections.ObjectModel;

namespace KeyboardStudio.Core;

/// <summary>Compares a mutable layout with an immutable import baseline by physical key identity.</summary>
public sealed class KeyboardLayoutDiffer
{
    private readonly ModifierLayer[] _layers = Enum.GetValues<ModifierLayer>();

    public KeyboardLayoutDifference Compare(
        KeyboardLayout current,
        IReadOnlyList<KeyMappingSnapshot> baseline)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);

        var currentByKey = IndexCurrent(current.Mappings);
        var baselineByKey = IndexBaseline(baseline);
        var keyIds = currentByKey.Keys
            .Concat(baselineByKey.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(keyId => keyId, StringComparer.Ordinal);
        var changes = new List<KeyboardKeyDifference>();

        foreach (var keyId in keyIds)
        {
            currentByKey.TryGetValue(keyId, out var currentMapping);
            baselineByKey.TryGetValue(keyId, out var baselineMapping);

            if (currentMapping is null)
            {
                changes.Add(new KeyboardKeyDifference(
                    keyId,
                    KeyboardMappingChangeKind.Removed,
                    baselineMapping,
                    Current: null,
                    LogicalKeyChanged: true,
                    ChangedLayers(baselineMapping!, null)));
                continue;
            }

            var currentSnapshot = KeyMappingSnapshot.From(currentMapping);
            if (baselineMapping is null)
            {
                changes.Add(new KeyboardKeyDifference(
                    keyId,
                    KeyboardMappingChangeKind.Added,
                    Baseline: null,
                    currentSnapshot,
                    LogicalKeyChanged: true,
                    ChangedLayers(null, currentSnapshot)));
                continue;
            }

            var logicalKeyChanged = baselineMapping.LogicalKey != currentSnapshot.LogicalKey;
            var changedLayers = ChangedLayers(baselineMapping, currentSnapshot);
            if (!logicalKeyChanged && changedLayers.Count == 0)
            {
                continue;
            }

            changes.Add(new KeyboardKeyDifference(
                keyId,
                KeyboardMappingChangeKind.Modified,
                baselineMapping,
                currentSnapshot,
                logicalKeyChanged,
                changedLayers));
        }

        return new KeyboardLayoutDifference(changes.AsReadOnly());
    }

    private static Dictionary<string, KeyMapping> IndexCurrent(IEnumerable<KeyMapping> mappings)
    {
        var result = new Dictionary<string, KeyMapping>(StringComparer.Ordinal);
        foreach (var mapping in mappings)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            if (!result.TryAdd(mapping.KeyId, mapping))
            {
                throw new ArgumentException(
                    $"Current layout contains more than one mapping for physical key '{mapping.KeyId}'.",
                    nameof(mappings));
            }
        }

        return result;
    }

    private static Dictionary<string, KeyMappingSnapshot> IndexBaseline(
        IEnumerable<KeyMappingSnapshot> mappings)
    {
        var result = new Dictionary<string, KeyMappingSnapshot>(StringComparer.Ordinal);
        foreach (var mapping in mappings)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            if (!result.TryAdd(mapping.KeyId, mapping))
            {
                throw new ArgumentException(
                    $"Import baseline contains more than one mapping for physical key '{mapping.KeyId}'.",
                    nameof(mappings));
            }
        }

        return result;
    }

    private ReadOnlyCollection<ModifierLayer> ChangedLayers(
        KeyMappingSnapshot? baseline,
        KeyMappingSnapshot? current)
    {
        var changed = new List<ModifierLayer>();
        foreach (var layer in _layers)
        {
            KeyOutput? baselineOutput = null;
            KeyOutput? currentOutput = null;
            var baselineHasValue = baseline is not null &&
                baseline.Outputs.TryGetValue(layer, out baselineOutput);
            var currentHasValue = current is not null &&
                current.Outputs.TryGetValue(layer, out currentOutput);
            if (baselineHasValue != currentHasValue ||
                baselineHasValue && !Equals(baselineOutput, currentOutput))
            {
                changed.Add(layer);
            }
        }

        return changed.AsReadOnly();
    }
}
