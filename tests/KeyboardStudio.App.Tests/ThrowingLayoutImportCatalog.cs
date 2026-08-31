using KeyboardStudio.Core;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// A catalog whose import throws whatever the test gave it.
/// </summary>
/// <remarks>
/// A failed import and an import that threw are different events on a real host — an unreadable
/// file is not a layout that composed badly — and only the second one can reach the window as an
/// exception.
/// </remarks>
internal sealed class ThrowingLayoutImportCatalog : ILayoutImportCatalog
{
    private readonly Func<Exception> _exception;

    public ThrowingLayoutImportCatalog(Func<Exception> exception) => _exception = exception;

    public bool HasAvailableSources => true;

    public Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ImportableLayoutDescriptor>>([]);

    public Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default) =>
        throw _exception();
}
