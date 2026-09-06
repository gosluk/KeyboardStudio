namespace KeyboardStudio.App;

/// <summary>
/// What came of trying to read the layout this host is configured to type with.
/// </summary>
public enum StartupLayoutStatus
{
    /// <summary>Nothing on this host can name or read a layout. Not a failure.</summary>
    Unavailable,

    /// <summary>A layout was detected and read.</summary>
    Imported,

    /// <summary>A layout was detected but could not be read.</summary>
    Failed,

    /// <summary>The load was abandoned before it finished.</summary>
    Cancelled,
}
