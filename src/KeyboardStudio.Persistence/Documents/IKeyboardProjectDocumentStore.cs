namespace KeyboardStudio.Persistence;

public interface IKeyboardProjectDocumentStore
{
    Task SaveAsync(
        KeyboardProjectDocument document,
        Stream destination,
        CancellationToken cancellationToken = default);

    Task<KeyboardProjectDocument> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default);
}
