namespace KeyboardStudio.Build;

public sealed class BuildWorkspace
{
    private BuildWorkspace(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        GeneratedDirectory = Path.Combine(rootDirectory, "generated");
        ObjectDirectory = Path.Combine(rootDirectory, "obj");
        OutputDirectory = Path.Combine(rootDirectory, "output");
        LogsDirectory = Path.Combine(rootDirectory, "logs");
    }

    public string RootDirectory { get; }
    public string GeneratedDirectory { get; }
    public string ObjectDirectory { get; }
    public string OutputDirectory { get; }
    public string LogsDirectory { get; }

    public static BuildWorkspace Create(string buildRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildRoot);
        var root = Path.Combine(Path.GetFullPath(buildRoot), $"build-{Guid.NewGuid():N}");
        var workspace = new BuildWorkspace(root);
        Directory.CreateDirectory(workspace.GeneratedDirectory);
        Directory.CreateDirectory(workspace.ObjectDirectory);
        Directory.CreateDirectory(workspace.OutputDirectory);
        Directory.CreateDirectory(workspace.LogsDirectory);
        return workspace;
    }

    public async Task WriteGeneratedSourceAsync(
        GeneratedSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var file in source.Files.OrderBy(file => file.Key, StringComparer.Ordinal))
        {
            ValidateGeneratedFileName(file.Key);
            var path = Path.Combine(GeneratedDirectory, file.Key);
            await File.WriteAllTextAsync(path, file.Value, cancellationToken);
        }
    }

    public void DeleteIntermediates()
    {
        DeleteDirectory(GeneratedDirectory);
        DeleteDirectory(ObjectDirectory);
    }

    public void Delete() => DeleteDirectory(RootDirectory);

    private static void ValidateGeneratedFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            Path.IsPathRooted(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Generated source file names must be non-empty leaf names.",
                nameof(fileName));
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
