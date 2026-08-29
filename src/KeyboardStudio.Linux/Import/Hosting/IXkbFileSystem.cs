namespace KeyboardStudio.Linux;

/// <summary>
/// The parts of the filesystem the import pipeline reads. Import never writes, so this deliberately
/// exposes no way to create or modify anything.
/// </summary>
public interface IXkbFileSystem
{
    /// <summary>Whether a directory exists at <paramref name="path"/>.</summary>
    bool DirectoryExists(string path);

    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>
    /// Full paths of the files directly inside <paramref name="path"/>, or an empty list when the
    /// directory does not exist. A missing directory is ordinary on a host that ships only part of
    /// the XKB database, so it is not an error.
    /// </summary>
    IReadOnlyList<string> EnumerateFiles(string path);

    /// <summary>Opens a file for reading.</summary>
    Stream OpenRead(string path);
}
