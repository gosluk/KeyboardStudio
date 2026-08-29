namespace KeyboardStudio.Core;

/// <summary>
/// The standard <see cref="ILayoutImportCatalog"/>: a thin aggregator over the sources registered at
/// the composition root. It holds no layout knowledge of its own.
/// </summary>
public sealed class LayoutImportCatalog : ILayoutImportCatalog
{
    private readonly IReadOnlyList<ILayoutImportSource> _sources;

    /// <summary>
    /// Registers the sources, in the order they should be listed.
    /// </summary>
    /// <exception cref="ArgumentException">Two sources share an <see cref="ILayoutImportSource.Id"/>.</exception>
    public LayoutImportCatalog(IEnumerable<ILayoutImportSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var registered = new List<ILayoutImportSource>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (!seenIds.Add(source.Id))
            {
                // Source IDs are written into saved documents as provenance, so a collision would
                // make an imported document's origin ambiguous forever after.
                throw new ArgumentException(
                    $"More than one layout import source uses the ID '{source.Id}'.",
                    nameof(sources));
            }

            registered.Add(source);
        }

        _sources = registered;
    }

    /// <inheritdoc />
    public bool HasAvailableSources
    {
        get
        {
            foreach (var source in _sources)
            {
                if (source.IsAvailable)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var descriptors = new List<ImportableLayoutDescriptor>();
        foreach (var source in _sources)
        {
            // An unavailable source is skipped rather than queried. A source that is available but
            // then fails is a different matter and is allowed to propagate: silently returning a
            // shorter list would leave the user hunting for a layout with no clue why it is absent.
            if (!source.IsAvailable)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            descriptors.AddRange(await source.ListAsync(cancellationToken));
        }

        return descriptors;
    }

    /// <inheritdoc />
    public Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var source in _sources)
        {
            if (source.IsAvailable && string.Equals(source.Id, reference.SourceId, StringComparison.Ordinal))
            {
                return source.ImportAsync(reference, options, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"No available layout import source has the ID '{reference.SourceId}'.");
    }
}
