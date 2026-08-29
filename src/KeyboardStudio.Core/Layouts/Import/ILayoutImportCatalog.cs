namespace KeyboardStudio.Core;

/// <summary>
/// The aggregate view of every registered <see cref="ILayoutImportSource"/>, and the only import
/// type the presentation layer sees.
///
/// It plays the part <c>IBuildBackendResolver</c> plays for builds: the composition root knows which
/// concrete sources exist, and everything above it works in terms of descriptors and references. A
/// second source can be added later without reshaping the editor.
/// </summary>
public interface ILayoutImportCatalog
{
    /// <summary>
    /// Whether any registered source is usable on this host. False means import should be offered
    /// as unavailable rather than as an action that will fail.
    /// </summary>
    bool HasAvailableSources { get; }

    /// <summary>
    /// Lists every layout the available sources can import, in source registration order.
    /// </summary>
    Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports one layout through the source named by <see cref="ImportableLayoutReference.SourceId"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No available source carries that ID. This is a wiring fault, not import loss, so it throws
    /// instead of returning a failed result.
    /// </exception>
    Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default);
}
