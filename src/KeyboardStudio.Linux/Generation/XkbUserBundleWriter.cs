using System.Collections.ObjectModel;
using System.Text;

namespace KeyboardStudio.Linux;

/// <summary>Writes a generated bundle beneath build output, never to a live XDG root.</summary>
public sealed class XkbUserBundleWriter : IXkbUserBundleWriter
{
    private const string BundleManifestFileName = "keyboardstudio-bundle.json";

    public async Task<XkbUserBundleWriteResult> WriteAsync(
        XkbGeneratedUserBundle bundle,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        var fullOutputRoot = Path.GetFullPath(outputRoot);
        var bundleRoot = Path.Combine(fullOutputRoot, "xkb-user-bundle");

        // Every path is resolved before anything is written, so a rejected path leaves the previous
        // bundle whole instead of half replaced.
        var planned = new List<(string Path, XkbUserBundleFile File)>(bundle.Files.Count);
        foreach (var file in bundle.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            planned.Add((ControlledPath(bundleRoot, file.RelativePath), file));
        }

        var removed = PruneSupersededFiles(
            bundleRoot,
            planned.Select(item => item.Path).ToHashSet(StringComparer.Ordinal));
        var written = new List<string>(planned.Count);
        foreach (var (path, file) in planned)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                file.Content,
                new UTF8Encoding(false),
                cancellationToken);
            written.Add(path);
        }

        return new XkbUserBundleWriteResult(bundleRoot, written.AsReadOnly(), removed);
    }

    private static string ControlledPath(string bundleRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Bundle path '{relativePath}' is not a portable relative path.");
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 ||
            segments.Any(segment => segment is "." or ".." || segment.Contains(':', StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Bundle path '{relativePath}' is not a safe relative path.");
        }

        var path = Path.GetFullPath(Path.Combine([bundleRoot, .. segments]));
        if (!path.StartsWith(bundleRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Bundle path '{relativePath}' escapes the output root.");
        }

        return path;
    }

    /// <summary>
    /// Removes what an earlier generation left behind, so the bundle root holds this bundle and
    /// nothing else: a project that stops deriving from a base layout, or is renamed onto another
    /// one, would otherwise ship a stale bridge beside its current files and install it. Only a
    /// root carrying the generated bundle manifest is pruned, which keeps an unrelated directory
    /// that happens to sit at this path untouched.
    /// </summary>
    private static ReadOnlyCollection<string> PruneSupersededFiles(
        string bundleRoot,
        HashSet<string> current)
    {
        if (!File.Exists(Path.Combine(bundleRoot, BundleManifestFileName)))
        {
            return ReadOnlyCollection<string>.Empty;
        }

        var removed = new List<string>();
        Prune(bundleRoot, current, removed);
        return removed.AsReadOnly();
    }

    private static void Prune(string directory, HashSet<string> current, List<string> removed)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            // A link is never followed and never deleted through: pruning stays inside the tree
            // this writer created.
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Prune(entry, current, removed);
                if (!Directory.EnumerateFileSystemEntries(entry).Any())
                {
                    Directory.Delete(entry);
                }

                continue;
            }

            if (!current.Contains(entry))
            {
                File.Delete(entry);
                removed.Add(entry);
            }
        }
    }
}
