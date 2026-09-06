namespace KeyboardStudio.App;

/// <summary>
/// Reads the layout this host is already configured to type with.
/// </summary>
public interface IStartupLayoutLoader
{
    /// <summary>
    /// Detects and imports the current layout. Every outcome, including failure, comes back as a
    /// result: nothing here throws into a window that is already drawing a working document.
    /// </summary>
    Task<StartupLayoutResult> LoadAsync(CancellationToken cancellationToken = default);
}
