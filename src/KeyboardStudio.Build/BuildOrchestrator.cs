using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed record KeyboardBuildResult(
    bool Success,
    IReadOnlyList<ValidationIssue> ValidationIssues,
    CompilationResult? Compilation);

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
        var issues = _validator.Validate(project);
        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return new KeyboardBuildResult(false, issues, null);
        }

        if (!_environment.CanBuild(options.Target))
        {
            var status = _environment.GetStatus(options.Target);
            var compilation = new CompilationResult(
                false,
                null,
                [new CompilerMessage("ENV001", status.Message)]);
            return new KeyboardBuildResult(false, issues, compilation);
        }

        var generated = await _generator.GenerateAsync(project, options, cancellationToken);
        var compilationResult = await _compiler.CompileAsync(generated.Source, options.Target, cancellationToken);
        return new KeyboardBuildResult(compilationResult.Success, issues, compilationResult);
    }
}
