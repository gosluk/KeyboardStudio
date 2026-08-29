namespace KeyboardStudio.Linux;

/// <summary>
/// Finds the XKB database directories available on this host.
/// </summary>
public interface IXkbDataRootLocator
{
    /// <summary>
    /// Returns the roots that exist, in the order libxkbcommon searches them: the first root
    /// defining a name is the one that wins. An empty list means the host has no XKB database,
    /// which is an ordinary state rather than a fault.
    /// </summary>
    IReadOnlyList<XkbDataRoot> Locate();
}
