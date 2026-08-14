namespace KeyboardStudio.Core;

public sealed class KeyboardLayout
{
    public List<KeyMapping> Mappings { get; init; } = [];

    public KeyMapping? Find(string keyId) =>
        Mappings.FirstOrDefault(mapping => string.Equals(mapping.KeyId, keyId, StringComparison.Ordinal));
}

public sealed class KeyMapping
{
    public required string KeyId { get; init; }
    public LogicalKey LogicalKey { get; set; }
    public Dictionary<ModifierLayer, KeyOutput> Outputs { get; init; } = [];
}
