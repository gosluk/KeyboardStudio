using System.Text;

namespace KeyboardStudio.Linux;

/// <summary>Writes a generated bundle beneath build output, never to a live XDG root.</summary>
public sealed class XkbUserBundleWriter : IXkbUserBundleWriter
{
    public async Task<XkbUserBundleWriteResult> WriteAsync(
        XkbGeneratedUserBundle bundle,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        var fullOutputRoot = Path.GetFullPath(outputRoot);
        var bundleRoot = Path.Combine(fullOutputRoot, "xkb-user-bundle");
        var written = new List<string>(bundle.Files.Count);

        foreach (var file in bundle.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Path.IsPathRooted(file.RelativePath) ||
                file.RelativePath.Contains('\\', StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Bundle path '{file.RelativePath}' is not a portable relative path.");
            }

            var segments = file.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 ||
                segments.Any(segment => segment is "." or ".." || segment.Contains(':', StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Bundle path '{file.RelativePath}' is not a safe relative path.");
            }

            var path = Path.GetFullPath(Path.Combine([bundleRoot, .. segments]));
            if (!path.StartsWith(bundleRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Bundle path '{file.RelativePath}' escapes the output root.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                file.Content,
                new UTF8Encoding(false),
                cancellationToken);
            written.Add(path);
        }

        return new XkbUserBundleWriteResult(bundleRoot, written.AsReadOnly());
    }
}
