namespace KeyboardStudio.App;

internal interface IApplicationSettingsFileSystem
{
    bool FileExists(string path);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);

    void CreateDirectory(string path);

    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken);

    void MoveFile(string sourcePath, string destinationPath, bool overwrite);

    void DeleteFile(string path);
}
