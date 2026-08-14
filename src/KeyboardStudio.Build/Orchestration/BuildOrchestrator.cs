using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed class BuildOrchestrator
{
    private readonly IKeyboardProjectValidator _validator;
    private readonly IArtifactGenerator _generator;
    private readonly IBuildEnvironment _environment;
    private readonly INativeCompiler _compiler;

    public BuildOrchestrator(
        IKeyboardProjectValidator validator,
        IArtifactGenerator generator,
        IBuildEnvironment environment,
        INativeCompiler compiler)
    {
        _validator = validator;
        _generator = generator;
        _environment = environment;
        _compiler = compiler;
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
        var compilationResult = await _compiler.CompileAsync(generated.Source, options.Target, cancellationToken);
        return new KeyboardBuildResult(compilationResult.Success, validation.Issues, compilationResult);
    }
}
