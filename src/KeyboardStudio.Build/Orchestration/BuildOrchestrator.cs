using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed class BuildOrchestrator
{
    private readonly IKeyboardProjectValidator _validator;
    private readonly IArtifactGenerator _generator;
    private readonly IBuildEnvironment _environment;
    private readonly INativeCompiler _compiler;
    private readonly IBuildManifestWriter _manifestWriter;

    public BuildOrchestrator(
        IKeyboardProjectValidator validator,
        IArtifactGenerator generator,
        IBuildEnvironment environment,
        INativeCompiler compiler,
        IBuildManifestWriter? manifestWriter = null)
    {
        _validator = validator;
        _generator = generator;
        _environment = environment;
        _compiler = compiler;
        _manifestWriter = manifestWriter ?? new JsonBuildManifestWriter();
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
        if (compilationResult.Success)
        {
            try
            {
                var manifestResult = await _manifestWriter.WriteAsync(
                    project,
                    generated,
                    options,
                    compilationResult,
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

        return new KeyboardBuildResult(compilationResult.Success, validation.Issues, compilationResult);
    }
}
