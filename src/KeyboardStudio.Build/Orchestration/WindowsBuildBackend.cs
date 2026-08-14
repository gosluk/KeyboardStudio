using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed class WindowsBuildBackend : IBuildBackend
{
    private static readonly HashSet<BuildTarget> Targets =
    [
        BuildTarget.WindowsX64,
        BuildTarget.WindowsArm64
    ];

    private readonly IArtifactGenerator _generator;
    private readonly IBuildEnvironment _environment;
    private readonly INativeCompiler _compiler;
    private readonly IBuildManifestWriter _manifestWriter;
    private readonly IBuildReproducibilityChecker _reproducibilityChecker;

    public WindowsBuildBackend(
        IArtifactGenerator generator,
        IBuildEnvironment environment,
        INativeCompiler compiler,
        IBuildManifestWriter? manifestWriter = null,
        IBuildReproducibilityChecker? reproducibilityChecker = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _manifestWriter = manifestWriter ?? new JsonBuildManifestWriter();
        _reproducibilityChecker = reproducibilityChecker ?? new BuildReproducibilityChecker();
    }

    public IReadOnlySet<BuildTarget> SupportedTargets => Targets;

    public BuildEnvironmentStatus GetStatus(BuildTarget target)
    {
        EnsureSupported(target);
        return _environment.GetStatus(target);
    }

    public async Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        EnsureSupported(options.Target);

        if (!_environment.CanBuild(options.Target))
        {
            var status = _environment.GetStatus(options.Target);
            var compilation = new CompilationResult(
                false,
                null,
                [new CompilerMessage("ENV001", status.Message)]);
            return new KeyboardBuildResult(false, [], compilation);
        }

        var generated = await _generator.GenerateAsync(project, options, cancellationToken);
        var compilationResult = await _compiler.CompileAsync(generated, options, cancellationToken);
        BuildReproducibilityResult? reproducibility = null;
        if (compilationResult.Success)
        {
            if (options.VerifyReproducibility)
            {
                reproducibility = await VerifyReproducibilityAsync(
                    project,
                    generated,
                    compilationResult,
                    options,
                    cancellationToken);
            }

            try
            {
                var manifestResult = await _manifestWriter.WriteAsync(
                    project,
                    generated,
                    options,
                    compilationResult,
                    reproducibility,
                    cancellationToken);
                compilationResult = compilationResult with
                {
                    Manifest = manifestResult.Manifest,
                    ManifestPath = manifestResult.ManifestPath,
                    ArtifactSha256 = manifestResult.Manifest.Output.Sha256
                };
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                compilationResult = compilationResult with
                {
                    Success = false,
                    Messages =
                    [
                        .. compilationResult.Messages,
                        new CompilerMessage(
                            "MANIFEST_WRITE",
                            $"The build manifest could not be written: {exception.Message}")
                    ]
                };
            }
        }

        if (reproducibility is { Success: false })
        {
            compilationResult = compilationResult with
            {
                Success = false,
                Messages = [.. compilationResult.Messages, .. reproducibility.Messages]
            };
        }

        return new KeyboardBuildResult(
            compilationResult.Success,
            [],
            compilationResult,
            reproducibility);
    }

    private async Task<BuildReproducibilityResult> VerifyReproducibilityAsync(
        KeyboardProject project,
        GeneratedArtifact firstGeneratedArtifact,
        CompilationResult firstCompilation,
        BuildOptions options,
        CancellationToken cancellationToken)
    {
        var repeatedOptions = options with { VerifyReproducibility = false };
        var secondGeneratedArtifact = await _generator.GenerateAsync(project, repeatedOptions, cancellationToken);
        var secondCompilation = await _compiler.CompileAsync(secondGeneratedArtifact, repeatedOptions, cancellationToken);
        if (!secondCompilation.Success ||
            string.IsNullOrWhiteSpace(firstCompilation.ArtifactPath) ||
            string.IsNullOrWhiteSpace(secondCompilation.ArtifactPath))
        {
            var detail = secondCompilation.Messages.Count > 0
                ? secondCompilation.Messages[0].Message
                : "The repeated build did not return a valid artifact path.";
            return new BuildReproducibilityResult(
                false, false, false, null, null, secondCompilation.WorkspacePath,
                [new CompilerMessage("REPRO_BUILD", $"The repeated build failed: {detail}")]);
        }

        try
        {
            return await _reproducibilityChecker.CompareAsync(
                firstGeneratedArtifact,
                firstCompilation.ArtifactPath,
                secondGeneratedArtifact,
                secondCompilation.ArtifactPath,
                secondCompilation.WorkspacePath,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new BuildReproducibilityResult(
                false, false, false, null, null, secondCompilation.WorkspacePath,
                [new CompilerMessage("REPRO_BUILD", $"The repeated artifacts could not be compared: {exception.Message}")]);
        }
    }

    private static void EnsureSupported(BuildTarget target)
    {
        if (!Targets.Contains(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported Windows target.");
        }
    }
}
