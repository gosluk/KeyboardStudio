using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed class BuildOrchestrator
{
    private readonly IKeyboardProjectValidator _validator;
    private readonly IArtifactGenerator _generator;
    private readonly IBuildEnvironment _environment;
    private readonly INativeCompiler _compiler;
    private readonly IBuildManifestWriter _manifestWriter;
    private readonly IBuildReproducibilityChecker _reproducibilityChecker;

    public BuildOrchestrator(
        IKeyboardProjectValidator validator,
        IArtifactGenerator generator,
        IBuildEnvironment environment,
        INativeCompiler compiler,
        IBuildManifestWriter? manifestWriter = null,
        IBuildReproducibilityChecker? reproducibilityChecker = null)
    {
        _validator = validator;
        _generator = generator;
        _environment = environment;
        _compiler = compiler;
        _manifestWriter = manifestWriter ?? new JsonBuildManifestWriter();
        _reproducibilityChecker = reproducibilityChecker ?? new BuildReproducibilityChecker();
    }

    public async Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(project);
        if (validation.HasErrors)
        {
            return new KeyboardBuildResult(false, validation.Issues, null);
        }

        if (!_environment.CanBuild(options.Target))
        {
            var status = _environment.GetStatus(options.Target);
            var compilation = new CompilationResult(
                false,
                null,
                [new CompilerMessage("ENV001", status.Message)]);
            return new KeyboardBuildResult(false, validation.Issues, compilation);
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
            validation.Issues,
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
        var secondGeneratedArtifact = await _generator.GenerateAsync(
            project,
            repeatedOptions,
            cancellationToken);
        var secondCompilation = await _compiler.CompileAsync(
            secondGeneratedArtifact,
            repeatedOptions,
            cancellationToken);
        if (!secondCompilation.Success ||
            string.IsNullOrWhiteSpace(firstCompilation.ArtifactPath) ||
            string.IsNullOrWhiteSpace(secondCompilation.ArtifactPath))
        {
            var detail = secondCompilation.Messages.Count > 0
                ? secondCompilation.Messages[0].Message
                : "The repeated build did not return a valid artifact path.";
            return new BuildReproducibilityResult(
                false,
                false,
                false,
                null,
                null,
                secondCompilation.WorkspacePath,
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
                false,
                false,
                false,
                null,
                null,
                secondCompilation.WorkspacePath,
                [new CompilerMessage(
                    "REPRO_BUILD",
                    $"The repeated artifacts could not be compared: {exception.Message}")]);
        }
    }
}
