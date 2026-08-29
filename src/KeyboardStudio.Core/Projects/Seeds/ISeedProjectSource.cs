namespace KeyboardStudio.Core;

/// <summary>
/// Supplies the starting content of a new document. A new document is never empty:
/// callers receive a fully mapped project rather than bare geometry.
/// </summary>
public interface ISeedProjectSource
{
    /// <summary>
    /// Identifiers this source can create, in presentation order.
    /// </summary>
    IReadOnlyList<string> SeedIds { get; }

    /// <summary>
    /// Creates a new, independent project from the named seed. Every call returns a
    /// distinct object graph, so edits to one document never reach another.
    /// </summary>
    /// <exception cref="SeedProjectException">
    /// The seed is unknown, or its stored content could not be read.
    /// </exception>
    KeyboardProject Create(string seedId);
}
