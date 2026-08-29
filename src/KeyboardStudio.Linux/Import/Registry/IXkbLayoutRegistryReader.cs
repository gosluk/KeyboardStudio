namespace KeyboardStudio.Linux;

/// <summary>
/// Reads the human-readable layout descriptions a data root publishes.
/// </summary>
public interface IXkbLayoutRegistryReader
{
    /// <summary>
    /// Returns every layout and variant the root describes, layouts before their own variants and
    /// otherwise in file order. A root with no registry files yields an empty list; a root whose
    /// registry cannot be parsed throws, because silently listing nothing would leave the user
    /// hunting for a layout they know is installed.
    /// </summary>
    IReadOnlyList<XkbRegistryEntry> Read(XkbDataRoot root);
}
