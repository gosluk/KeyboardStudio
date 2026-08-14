namespace KeyboardStudio.Core;

public sealed class KeyboardEditor
{
    public KeyboardEditor(KeyboardProject project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
    }

    public KeyboardProject Project { get; }

    public KeyMapping GetOrCreateMapping(string keyId)
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

    public void MapCharacter(string keyId, ModifierLayer layer, string character)
    {
        GetOrCreateMapping(keyId).Outputs[layer] = new CharacterOutput(character);
    }

    public void MapLogicalKey(string keyId, LogicalKey key)
    {
        GetOrCreateMapping(keyId).LogicalKey = key;
    }

    public void ClearMapping(string keyId, ModifierLayer layer)
    {
        EnsurePhysicalKeyExists(keyId);
        Project.Layout.Find(keyId)?.Outputs.Remove(layer);
    }

    public void ClearAllOutputs(string keyId)
    {
        EnsurePhysicalKeyExists(keyId);
        Project.Layout.Find(keyId)?.Outputs.Clear();
    }

    private void EnsurePhysicalKeyExists(string keyId)
    {
        if (!Project.Keyboard.Keys.Any(key => string.Equals(key.Id, keyId, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Unknown physical key '{keyId}'.", nameof(keyId));
        }
    }
}
