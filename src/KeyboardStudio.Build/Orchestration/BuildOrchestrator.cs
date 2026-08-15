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
        IProgress<BuildStageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);

        progress?.Report(new BuildStageProgress(BuildStageNames.Validating, BuildStageState.Running));
        var validation = _validator.Validate(project);
        if (validation.HasErrors)
        {
            progress?.Report(new BuildStageProgress(BuildStageNames.Validating, BuildStageState.Failed));
            progress?.Report(new BuildStageProgress(BuildStageNames.Failed, BuildStageState.Failed));
            return new KeyboardBuildResult(false, validation.Issues, null);
        }

        progress?.Report(new BuildStageProgress(BuildStageNames.Validating, BuildStageState.Completed));

        var backend = _backendResolver.Resolve(options.Target);
        try
        {
            var result = await backend.BuildAsync(project, options, progress, cancellationToken);
            progress?.Report(new BuildStageProgress(
                result.Success ? BuildStageNames.Completed : BuildStageNames.Failed,
                result.Success ? BuildStageState.Completed : BuildStageState.Failed));
            return result with { ValidationIssues = validation.Issues };
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new BuildStageProgress(BuildStageNames.Cancelled, BuildStageState.Cancelled));
            throw;
        }
    }
}
