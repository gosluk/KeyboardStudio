namespace KeyboardStudio.Core;

public interface IKeyboardProjectStore
{
    Task SaveAsync(KeyboardProject project, Stream destination, CancellationToken cancellationToken = default);
    Task<KeyboardProject> LoadAsync(Stream source, CancellationToken cancellationToken = default);
}
