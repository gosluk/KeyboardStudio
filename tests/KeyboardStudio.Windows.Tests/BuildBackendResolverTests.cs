using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class BuildBackendResolverTests
{
    [Fact]
    public void Resolve_ReturnsTheOnlyBackendForTarget()
    {
        var backend = new StubBackend(BuildTarget.WindowsX64);
        var resolver = new BuildBackendResolver([backend]);

        Assert.Same(backend, resolver.Resolve(BuildTarget.WindowsX64));
    }

    [Fact]
    public void Constructor_WhenTargetHasMultipleBackends_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BuildBackendResolver([
            new StubBackend(BuildTarget.WindowsX64),
            new StubBackend(BuildTarget.WindowsX64)
        ]));
    }

    private sealed class StubBackend(BuildTarget target) : IBuildBackend
    {
        public IReadOnlySet<BuildTarget> SupportedTargets { get; } = new HashSet<BuildTarget> { target };

        public BuildEnvironmentStatus GetStatus(BuildTarget selectedTarget) =>
            new(true, "Available", [], [selectedTarget]);

        public Task<KeyboardBuildResult> BuildAsync(
            KeyboardProject project,
            BuildOptions options,
            IProgress<BuildStageProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new KeyboardBuildResult(true, [], null));
    }
}
