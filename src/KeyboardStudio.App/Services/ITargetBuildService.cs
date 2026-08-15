using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public interface ITargetBuildService
{
    BuildEnvironmentStatus GetEnvironmentStatus(BuildTarget target);

    Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        IReadOnlyDictionary<string, string> profileSettings,
        CancellationToken cancellationToken = default);
}
