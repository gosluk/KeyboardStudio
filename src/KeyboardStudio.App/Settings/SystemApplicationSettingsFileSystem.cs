namespace KeyboardStudio.App;

internal sealed class SystemApplicationSettingsFileSystem : IApplicationSettingsFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(path, contents, cancellationToken);

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Move(sourcePath, destinationPath, overwrite);

    public void DeleteFile(string path) => File.Delete(path);
}
