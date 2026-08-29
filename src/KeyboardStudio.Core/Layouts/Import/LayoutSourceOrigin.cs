namespace KeyboardStudio.Core;

/// <summary>
/// Where an importable layout came from. The editor groups and filters the catalog by origin, and
/// records it as provenance, so a user can tell a layout the distribution installed from one they
/// wrote themselves.
/// </summary>
public enum LayoutSourceOrigin
{
    /// <summary>Installed with the operating system or a distribution package.</summary>
    System = 0,

    /// <summary>Installed for the current user only.</summary>
    User = 1,

    /// <summary>Read from a file the user pointed at directly, outside any catalog.</summary>
    File = 2
}
