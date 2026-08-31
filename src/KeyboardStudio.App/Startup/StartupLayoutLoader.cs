using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// Detects the host's active layout and imports it.
/// </summary>
/// <remarks>
/// Separated from the document owner so the two questions stay apart: what this host types with,
/// and whether the answer may replace what is on screen. Nobody asked for this import, so nothing
/// it can hit is worth an exception or a dialog — a host with no keyboard database, a layout that
/// cannot be read, and a cancelled load are all ordinary results.
/// </remarks>
public sealed class StartupLayoutLoader : IStartupLayoutLoader
{
    private readonly ILayoutImportCatalog _catalog;
    private readonly IHostLayoutProbe _probe;

    public StartupLayoutLoader(ILayoutImportCatalog catalog, IHostLayoutProbe probe)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(probe);

        _catalog = catalog;
        _probe = probe;
    }

    public async Task<StartupLayoutResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_catalog.HasAvailableSources)
        {
            // Nothing here can list a layout, so there is nothing to detect and nothing to report
            // about not having detected it.
            return StartupLayoutResult.Unavailable();
        }

        var reference = _probe.Detect();
        if (reference is null)
        {
            return StartupLayoutResult.Unavailable();
        }

        LayoutImportResult result;
        try
        {
            // Onto the thread pool in one hop. A source composes a layout from files as it is asked
            // for it and hands back a task that is already finished, so awaiting it on the UI
            // thread would hold the window for the whole of the work rather than none of it.
            result = await Task.Run(
                () => _catalog.ImportAsync(reference, LayoutImportOptions.Default, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return StartupLayoutResult.Cancelled();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return StartupLayoutResult.Failed(reference, exception.Message);
        }

        return result is { Success: true, Project: { } imported }
            ? StartupLayoutResult.Imported(reference, imported)
            : StartupLayoutResult.Failed(reference, "it could not be read on this host");
    }
}
