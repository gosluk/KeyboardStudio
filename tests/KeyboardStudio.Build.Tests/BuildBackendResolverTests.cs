using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Build.Tests;

public sealed class BuildBackendResolverTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ReturnsTheOnlyBackendForTarget()
    {
        var backend = new StubBackend(BuildTarget.LinuxXkb);
        var resolver = new BuildBackendResolver([backend]);

        Assert.Same(backend, resolver.Resolve(BuildTarget.LinuxXkb));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WhenTargetHasMultipleBackends_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BuildBackendResolver([
            new StubBackend(BuildTarget.LinuxXkb),
            new StubBackend(BuildTarget.LinuxXkb)
        ]));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenNoBackendSupportsTarget_Throws()
    {
        var resolver = new BuildBackendResolver([]);

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(BuildTarget.LinuxXkb));
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
