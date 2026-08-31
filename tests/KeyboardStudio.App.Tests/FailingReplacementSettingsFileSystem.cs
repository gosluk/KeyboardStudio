namespace KeyboardStudio.App.Tests;

/// <summary>
/// A real file system whose final replacement step always fails.
/// </summary>
/// <remarks>
/// The point of writing to a temporary file and renaming it is that an interrupted save leaves the
/// previous complete settings file intact. That guarantee can only be proven by interrupting the
/// rename, which the real file system will not do on request.
/// </remarks>
internal sealed class FailingReplacementSettingsFileSystem : IApplicationSettingsFileSystem
{
    private readonly SystemApplicationSettingsFileSystem _inner = new();

    public bool FileExists(string path) => _inner.FileExists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        _inner.ReadAllTextAsync(path, cancellationToken);

    public void CreateDirectory(string path) => _inner.CreateDirectory(path);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken) =>
        _inner.WriteAllTextAsync(path, contents, cancellationToken);

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite) =>
        throw new IOException("The replacement was interrupted.");

    public void DeleteFile(string path) => _inner.DeleteFile(path);
}
