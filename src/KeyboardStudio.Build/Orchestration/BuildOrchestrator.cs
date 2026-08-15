using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public sealed class BuildOrchestrator
{
    private readonly IKeyboardProjectValidator _validator;
    private readonly IBuildBackendResolver _backendResolver;

    public BuildOrchestrator(
        IKeyboardProjectValidator validator,
        IBuildBackendResolver backendResolver)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _backendResolver = backendResolver ?? throw new ArgumentNullException(nameof(backendResolver));
    }

    public async Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);

        var validation = _validator.Validate(project);
        if (validation.HasErrors)
        {
            return new KeyboardBuildResult(false, validation.Issues, null);
        }

        var backend = _backendResolver.Resolve(options.Target);
        var result = await backend.BuildAsync(project, options, cancellationToken);
        return result with { ValidationIssues = validation.Issues };
    }
}
