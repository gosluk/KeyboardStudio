using System.Security.Cryptography;

namespace KeyboardStudio.Build;

public sealed class BuildReproducibilityChecker : IBuildReproducibilityChecker
{
    public async Task<BuildReproducibilityResult> CompareAsync(
        GeneratedArtifact firstGeneratedArtifact,
        string firstArtifactPath,
        GeneratedArtifact secondGeneratedArtifact,
        string secondArtifactPath,
        string? comparisonWorkspacePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(firstGeneratedArtifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstArtifactPath);
        ArgumentNullException.ThrowIfNull(secondGeneratedArtifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondArtifactPath);
        cancellationToken.ThrowIfCancellationRequested();

        var generatedSourcesMatch = SourcesMatch(
            firstGeneratedArtifact.Source,
            secondGeneratedArtifact.Source);
        var firstHash = await HashFileAsync(firstArtifactPath, cancellationToken);
        var secondHash = await HashFileAsync(secondArtifactPath, cancellationToken);
        var binaryOutputsMatch = string.Equals(firstHash, secondHash, StringComparison.Ordinal);
        var messages = new List<CompilerMessage>();
        if (!generatedSourcesMatch)
        {
            messages.Add(new CompilerMessage(
                "REPRO_SOURCE",
                "Repeated generation produced different source file names or contents."));
        }

        if (!binaryOutputsMatch)
        {
            messages.Add(new CompilerMessage(
                "REPRO_BINARY",
                "Repeated native compilation produced a different DLL SHA-256 hash."));
        }

        return new BuildReproducibilityResult(
            messages.Count == 0,
            generatedSourcesMatch,
            binaryOutputsMatch,
            firstHash,
            secondHash,
            comparisonWorkspacePath,
            messages);
    }

    private static bool SourcesMatch(GeneratedSource first, GeneratedSource second)
    {
        if (first.Files.Count != second.Files.Count)
        {
            return false;
        }

        foreach (var file in first.Files)
        {
            if (!second.Files.TryGetValue(file.Key, out var secondContent) ||
                !string.Equals(file.Value, secondContent, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
