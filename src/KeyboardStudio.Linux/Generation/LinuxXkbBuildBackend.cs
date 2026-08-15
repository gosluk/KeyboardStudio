using System.Security.Cryptography;
using System.Text;
using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

public sealed class LinuxXkbBuildBackend : IBuildBackend
{
    private static readonly HashSet<BuildTarget> Targets = [BuildTarget.LinuxXkb];
    private readonly XkbLayoutMetadata _metadata;
    private readonly XkbLayoutTranslator _translator;
    private readonly IXkbSymbolsGenerator _generator;
    private readonly IXkbBuildManifestWriter _manifestWriter;
    private readonly IXkbArtifactVerifier _verifier;
    private readonly IXkbCliLocator _cliLocator;
    private readonly bool _requireExternalVerification;
    private readonly TimeProvider _timeProvider;

    public LinuxXkbBuildBackend(
        XkbLayoutMetadata metadata,
        XkbLayoutTranslator? translator = null,
        IXkbSymbolsGenerator? generator = null,
        IXkbBuildManifestWriter? manifestWriter = null,
        IXkbArtifactVerifier? verifier = null,
        IXkbCliLocator? cliLocator = null,
        bool requireExternalVerification = false,
        TimeProvider? timeProvider = null)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _translator = translator ?? new XkbLayoutTranslator();
        _generator = generator ?? new XkbSymbolsGenerator();
        _manifestWriter = manifestWriter ?? new XkbBuildManifestWriter();
        _verifier = verifier ?? new XkbArtifactVerifier();
        _cliLocator = cliLocator ?? new PathXkbCliLocator();
        _requireExternalVerification = requireExternalVerification;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlySet<BuildTarget> SupportedTargets => Targets;

    public BuildEnvironmentStatus GetStatus(BuildTarget target)
    {
        EnsureSupported(target);
        var cliPath = _cliLocator.Find();
        if (cliPath is null)
        {
            return new BuildEnvironmentStatus(
                true,
                "Linux XKB generation is available; optional xkbcli verification is unavailable.",
                [new BuildEnvironmentDiagnostic(
                    "KSL004",
                    "xkbcli was not found. The symbols file will still be generated and managed validation will run.")],
                [BuildTarget.LinuxXkb]);
        }

        return new BuildEnvironmentStatus(
            true,
            $"Linux XKB generation and xkbcli verification are available ({cliPath}).",
            [],
            [BuildTarget.LinuxXkb]);
    }

    public async Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        IProgress<BuildStageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        EnsureSupported(options.Target);
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new BuildStageProgress(BuildStageNames.GeneratingXkb, BuildStageState.Running));
        var translation = _translator.Translate(project, _metadata);
        if (!translation.Success)
        {
            progress?.Report(new BuildStageProgress(BuildStageNames.GeneratingXkb, BuildStageState.Failed));
            return Failure(translation.Diagnostics.Select(diagnostic => new BuildArtifactDiagnostic(
                BuildDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.KeyId)).ToArray());
        }

        try
        {
            var generated = _generator.Generate(translation.Layout!);
            progress?.Report(new BuildStageProgress(BuildStageNames.GeneratingXkb, BuildStageState.Completed));
            progress?.Report(new BuildStageProgress(BuildStageNames.WritingArtifact, BuildStageState.Running));
            var outputRoot = Path.GetFullPath(Path.Combine(options.OutputDirectory, "xkb"));
            var artifactPath = GetControlledArtifactPath(outputRoot, generated.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            await File.WriteAllTextAsync(
                artifactPath,
                generated.Content,
                new UTF8Encoding(false),
                cancellationToken);
            progress?.Report(new BuildStageProgress(BuildStageNames.WritingArtifact, BuildStageState.Completed));
            progress?.Report(new BuildStageProgress(BuildStageNames.Verifying, BuildStageState.Running));
            var verification = await _verifier.VerifyAsync(
                translation.Layout!,
                generated,
                outputRoot,
                _requireExternalVerification,
                cancellationToken);
            progress?.Report(new BuildStageProgress(
                BuildStageNames.Verifying,
                verification.Status == XkbVerificationStatus.Failed
                    ? BuildStageState.Failed
                    : BuildStageState.Completed));
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(generated.Content)))
                .ToLowerInvariant();
            var manifest = new XkbBuildManifest(
                1,
                project.Metadata.Name,
                BuildTarget.LinuxXkb,
                _metadata.LayoutId,
                _metadata.SectionId,
                XkbSymbolsGenerator.GeneratorVersion,
                artifactPath,
                hash,
                verification.Status.ToString(),
                verification.ToolVersion,
                _timeProvider.GetUtcNow());
            var manifestPath = await _manifestWriter.WriteAsync(
                manifest,
                outputRoot,
                cancellationToken);

            var details = new XkbBuildDetails(manifest, manifestPath, generated, verification);
            var success = verification.Status != XkbVerificationStatus.Failed;
            var artifact = new ArtifactBuildResult(
                success,
                artifactPath,
                verification.Diagnostics,
                verification.StandardOutput + verification.StandardError,
                verification.LogPath,
                ManifestPath: manifestPath,
                ArtifactSha256: hash,
                BackendDetails: details);
            return new KeyboardBuildResult(success, [], artifact);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            progress?.Report(new BuildStageProgress(BuildStageNames.WritingArtifact, BuildStageState.Failed));
            return Failure([
                new BuildArtifactDiagnostic(
                    BuildDiagnosticSeverity.Error,
                    "KSL006",
                    $"The XKB artifact could not be materialized: {exception.Message}")
            ]);
        }
    }

    private static string GetControlledArtifactPath(string outputRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("The generated XKB path must be relative.", nameof(relativePath));
        }

        var artifactPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath));
        var prefix = outputRoot.EndsWith(Path.DirectorySeparatorChar)
            ? outputRoot
            : outputRoot + Path.DirectorySeparatorChar;
        if (!artifactPath.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("The generated XKB path escapes the output directory.", nameof(relativePath));
        }

        return artifactPath;
    }

    private static KeyboardBuildResult Failure(IReadOnlyList<BuildArtifactDiagnostic> diagnostics) =>
        new(false, [], new ArtifactBuildResult(false, null, diagnostics));

    private static void EnsureSupported(BuildTarget target)
    {
        if (target != BuildTarget.LinuxXkb)
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported Linux target.");
        }
    }

}
