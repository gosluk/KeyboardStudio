namespace KeyboardStudio.Core;

/// <summary>
/// One place layouts can be imported from — the host's installed keyboard database, a file the user
/// picked, or anything else a platform assembly cares to implement.
///
/// Implementations live outside Core and are registered at the composition root. Everything crossing
/// this boundary is a <see cref="KeyboardProject"/> or an opaque identifier, so the domain never
/// learns a platform's layout vocabulary.
/// </summary>
public interface ILayoutImportSource
{
    /// <summary>
    /// Stable identifier, unique among registered sources. It is recorded as provenance on imported
    /// documents, so changing it orphans the provenance of documents already saved.
    /// </summary>
    string Id { get; }

    /// <summary>Human-readable name, shown when the catalog groups entries by source.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this source can be used on the current host. A source whose data is absent reports
    /// <see langword="false"/> instead of throwing, and the catalog passes over it: a host with no
    /// layout database is an ordinary situation, not a failure.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Lists everything this source can import. May be slow and may touch the filesystem, which is
    /// why it is asynchronous and cancellable; the catalogs involved run to several hundred entries.
    /// </summary>
    Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports one layout. Returns an unsuccessful <see cref="LayoutImportResult"/> rather than
    /// throwing when the layout cannot be read, so the caller can show the user why.
    /// </summary>
    /// <param name="reference">Which layout to import, normally from <see cref="ImportableLayoutDescriptor.ToReference"/>.</param>
    /// <param name="options">Caller choices, or <see cref="LayoutImportOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels a long-running import.</param>
    Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default);
}
