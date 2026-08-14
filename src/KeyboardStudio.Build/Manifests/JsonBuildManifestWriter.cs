using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed class JsonBuildManifestWriter : IBuildManifestWriter
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly TimeProvider _timeProvider;

    public JsonBuildManifestWriter()
        : this(TimeProvider.System)
    {
    }

    public JsonBuildManifestWriter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public async Task<BuildManifestWriteResult> WriteAsync(
        KeyboardProject project,
        GeneratedArtifact generatedArtifact,
        BuildOptions options,
        CompilationResult compilation,
        BuildReproducibilityResult? reproducibility,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(generatedArtifact);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(compilation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!compilation.Success ||
            string.IsNullOrWhiteSpace(compilation.ArtifactPath) ||
            !File.Exists(compilation.ArtifactPath))
        {
            throw new InvalidOperationException("A successful build artifact is required for a manifest.");
        }

        var toolchain = compilation.ToolchainVersions ?? throw new InvalidOperationException(
            "Toolchain versions are required for a build manifest.");
        var verification = compilation.Verification ?? throw new InvalidOperationException(
            "Artifact verification is required for a build manifest.");

        var generatedSources = generatedArtifact.Source.Files
            .OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => new BuildManifestFile(
                file.Key,
                HashBytes(Encoding.UTF8.GetBytes(file.Value))))
            .ToArray();
        var outputHash = await HashFileAsync(compilation.ArtifactPath, cancellationToken);
        var manifest = new BuildManifest(
            CurrentSchemaVersion,
            project.Metadata.Name,
            options.Target,
            generatedSources,
            toolchain,
            new BuildManifestFile(Path.GetFullPath(compilation.ArtifactPath), outputHash),
            new BuildVerificationManifest(
                verification.Machine,
                verification.IsDll,
                verification.ExpectedExportFound,
                verification.LoadTest.Status),
            reproducibility is null
                ? null
                : new BuildReproducibilityManifest(
                    reproducibility.Success,
                    reproducibility.GeneratedSourcesMatch,
                    reproducibility.BinaryOutputsMatch,
                    reproducibility.FirstArtifactSha256,
                    reproducibility.SecondArtifactSha256),
            _timeProvider.GetUtcNow());

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(compilation.ArtifactPath)) ??
            throw new InvalidOperationException("The build artifact does not have an output directory.");
        var manifestPath = Path.Combine(outputDirectory, "build-manifest.json");
        var json = JsonSerializer.Serialize(manifest, SerializerOptions) + "\n";
        await File.WriteAllTextAsync(manifestPath, json, new UTF8Encoding(false), cancellationToken);
        return new BuildManifestWriteResult(manifest, manifestPath);
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

    private static string HashBytes(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
