using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public interface IBuildBackend
{
    IReadOnlySet<BuildTarget> SupportedTargets { get; }

    BuildEnvironmentStatus GetStatus(BuildTarget target);

    Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        IProgress<BuildStageProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
