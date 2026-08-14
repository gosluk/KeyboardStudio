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
