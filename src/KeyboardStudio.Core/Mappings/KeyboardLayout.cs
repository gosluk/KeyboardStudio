namespace KeyboardStudio.Core;

public sealed class KeyboardLayout
{
    public List<KeyMapping> Mappings { get; init; } = [];

    public KeyMapping? Find(string keyId) =>
        Mappings.FirstOrDefault(mapping => string.Equals(mapping.KeyId, keyId, StringComparison.Ordinal));
}
