namespace KeyboardStudio.Core;

public sealed class KeyboardEditor
{
    public KeyboardEditor(KeyboardProject project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
    }

    public KeyboardProject Project { get; }

    public bool MapCharacter(string keyId, ModifierLayer layer, string character)
    {
        var output = new CharacterOutput(character);
        var mapping = GetOrCreateMapping(keyId);
        if (mapping.Outputs.TryGetValue(layer, out var current) && current == output)
        {
            return false;
        }

        mapping.Outputs[layer] = output;
        return true;
    }

    public bool MapLogicalKey(string keyId, LogicalKey key)
    {
        EnsurePhysicalKeyExists(keyId);
        var existing = Project.Layout.Find(keyId);
        if (existing is null && key == LogicalKey.None)
        {
            return false;
        }

        var mapping = existing ?? GetOrCreateMapping(keyId);
        if (mapping.LogicalKey == key)
        {
            return false;
        }

        mapping.LogicalKey = key;
        return true;
    }

    public bool ClearMapping(string keyId, ModifierLayer layer)
    {
        EnsurePhysicalKeyExists(keyId);
        return Project.Layout.Find(keyId)?.Outputs.Remove(layer) == true;
    }

    public bool ClearAllOutputs(string keyId)
    {
        EnsurePhysicalKeyExists(keyId);
        var outputs = Project.Layout.Find(keyId)?.Outputs;
        if (outputs is null || outputs.Count == 0)
        {
            return false;
        }

        outputs.Clear();
        return true;
    }

    /// <summary>
    /// Replaces the whole layout with <paramref name="mappings"/>, keeping the project's geometry.
    ///
    /// This is what commits an imported layout onto a document the user already has: the keyboard,
    /// the file it is saved as, and its build settings all stay, and only what the keys produce
    /// changes. A mapping naming a key this keyboard does not have is skipped rather than rejected,
    /// so a layout read on one geometry can be laid onto another; the count comes back so the
    /// caller can say how much did not fit.
    /// </summary>
    /// <param name="mappings">The mappings to keep. Each is copied, so the source stays independent.</param>
    /// <returns>How many mappings named a key this keyboard does not have.</returns>
    public int ReplaceMappings(IEnumerable<KeyMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);

        var keyIds = Project.Keyboard.Keys
            .Select(key => key.Id)
            .ToHashSet(StringComparer.Ordinal);

        // Built in full before anything is cleared, so a mapping that throws part way through
        // leaves the project with the layout it had rather than with half of a new one.
        var replacement = new List<KeyMapping>();
        var skipped = 0;

        foreach (var mapping in mappings)
        {
            ArgumentNullException.ThrowIfNull(mapping);

            if (!keyIds.Contains(mapping.KeyId))
            {
                skipped++;
                continue;
            }

            var copy = new KeyMapping
            {
                KeyId = mapping.KeyId,
                LogicalKey = mapping.LogicalKey
            };

            foreach (var (layer, output) in mapping.Outputs)
            {
                copy.Outputs[layer] = output;
            }

            replacement.Add(copy);
        }

        Project.Layout.Mappings.Clear();
        Project.Layout.Mappings.AddRange(replacement);
        return skipped;
    }

    private KeyMapping GetOrCreateMapping(string keyId)
    {
        EnsurePhysicalKeyExists(keyId);

        var mapping = Project.Layout.Find(keyId);
        if (mapping is not null)
        {
            return mapping;
        }

        mapping = new KeyMapping
        {
            KeyId = keyId,
            LogicalKey = LogicalKey.None
        };
        Project.Layout.Mappings.Add(mapping);
        return mapping;
    }

    private void EnsurePhysicalKeyExists(string keyId)
    {
        if (!Project.Keyboard.Keys.Any(key => string.Equals(key.Id, keyId, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Unknown physical key '{keyId}'.", nameof(keyId));
        }
    }
}
